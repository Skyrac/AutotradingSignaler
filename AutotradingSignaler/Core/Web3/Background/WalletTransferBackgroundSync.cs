﻿using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Contracts.Web3.Contracts;
using AutotradingSignaler.Contracts.Web3.Events;
using AutotradingSignaler.Core.Handlers.Commands.Web3;
using AutotradingSignaler.Persistence.Repositories;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;
using MediatR;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Util;
using System.Collections.Concurrent;

namespace AutotradingSignaler.Core.Web.Background
{
    public class WalletTransferBackgroundSync : BackgroundService
    {
        private readonly ILogger<WalletTransferBackgroundSync> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Web3Service _web3Service;
        private readonly ConcurrentQueue<UnprocessesSwapEvent> _unprocessedSwapEvents = new ConcurrentQueue<UnprocessesSwapEvent>();
        private readonly List<Watchlist> _watchlist = new List<Watchlist>();
        private readonly List<(StreamingWebSocketClient, EthLogsObservableSubscription)> subs = new List<(StreamingWebSocketClient, EthLogsObservableSubscription)>();
        private record UnprocessesSwapEvent(int chainId, FilterLog log);

        public WalletTransferBackgroundSync(ILogger<WalletTransferBackgroundSync> logger, Web3Service web3Service, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _web3Service = web3Service;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(6000);
            var unprocessed = new List<UnprocessesSwapEvent>();
            var counter = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (subs.Any(sub =>
                    {
                        return sub.Item1.WebSocketState != System.Net.WebSockets.WebSocketState.Open;
                    }))
                    {
                        await RestartSubs();
                    }
                    if (!subs.Any())
                    {
                        await RestartSubs();
                    }
                    //TODO: Check for new findings and execute process like notification  or trade execution

                    while (_unprocessedSwapEvents.TryDequeue(out var unprocessedEvent))
                    {
                        unprocessed.Add(unprocessedEvent);
                        if (_unprocessedSwapEvents.Count <= 0 || unprocessed.Count >= 100)
                        {
                            try
                            {
                                await ProcessSwapEvents(unprocessed, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Exception in BackgroundService while ProcessingSwapEvents: {nameof(WalletTransferBackgroundSync)} - {ex?.InnerException?.Message ?? ex?.Message}");
                            }
                        }

                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception in BackgroundService: {nameof(WalletTransferBackgroundSync)} - {ex?.InnerException?.Message ?? ex?.Message}");

                }

                counter = CheckAndUpdateWatchlist(counter);
            }
        }

        private async Task RestartSubs()
        {
            if (subs.Any())
            {
                subs.ForEach(async sub =>
                {
                    try
                    {
                        await sub.Item1.StopAsync();
                    }
                    catch (Exception ex)
                    {

                    }
                });
                await Task.Delay(TimeSpan.FromSeconds(3));
                subs.Clear();
            }
            foreach (var chain in _web3Service.GetWeb3Instances())
            {
                subs.Add(await GetLogsTokenTransfer_Observable_Subscription(chain.Key));
            }
        }

        private int CheckAndUpdateWatchlist(int counter)
        {
            counter++;
            if (counter > 100)
            {
                counter = 0;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IWeb3UnitOfWork>();
                    var watchlist = repository.Watchlist.GetAll().ToList();
                    if (watchlist.Any())
                    {
                        _watchlist.Clear();
                        _watchlist.AddRange(watchlist);
                    }
                }
            }
            return counter;
        }

        private List<TradingPlattform> GetTradingPlattforms(List<string> routers)
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWeb3UnitOfWork>();
            return repository.TradingPlattforms.Where(t => routers.Contains(t.Router)).GetAll().ToList();
        }

        private async Task ProcessSwapEvents(List<UnprocessesSwapEvent> unprocessed, CancellationToken cancellationToken)
        {
            await Task.Delay(500);
            var receiptDict = await GetTransactionReceipts(unprocessed);
            var allPlattforms = receiptDict.Values.Where(u => u.Response != null).Select(u => u.Response.To).Distinct().ToList();
            var existingPlattforms = GetTradingPlattforms(allPlattforms);
            var foundPlattforms = receiptDict.Where(t => t.Value != null && t.Value.Response != null && !t.Value.HasError).Select(v => new TradingPlattform { Router = v.Value.Response.To, ChainId = v.Key.chainId }).Distinct().ToList();
            existingPlattforms.AddRange(await StoreNewPlattforms(existingPlattforms, foundPlattforms));
            var trades = new List<Trade>();
            foreach (var result in receiptDict)
            {
                if (result.Value.HasError || result.Value.Response == null)
                {
                    _logger.LogWarning($"{result.Key.chainId}: No receipt for {result.Key.log.TransactionHash}");
                    continue;
                }
                var receipt = result.Value.Response;
                var from = receipt.From;
                var to = receipt.To;
                EventLog<TransferEventDTO> fromLog = null;  //Log of Token that was sent into swap
                EventLog<TransferEventDTO> toLog = null;    //Log of Token that was received from swap
                RetrieveTradeInformation(receipt, from, to, ref fromLog, ref toLog);

                if (fromLog != null && toLog != null)
                {
                    try
                    {
                        var trade = await ProcessTradeInformation(result.Key, receipt, from, to, fromLog, toLog, existingPlattforms, cancellationToken);
                        if (trade != null)
                        {
                            trades.Add(trade);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Exception in BackgroundService while processing trade information: {nameof(WalletTransferBackgroundSync)} - {ex?.InnerException?.Message ?? ex?.Message}");
                    }
                }
            }
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWeb3UnitOfWork>();
            var newTrades = trades.Select(t => t.TxHash).ToList();
            var existingTrades = repository.Trades.Where(t => newTrades.Contains(t.TxHash)).GetAll().Select(t => new { t.ChainId, t.TxHash });

            foreach (var trade in trades.Where(t => !existingTrades.Any(e => e.ChainId == t.ChainId && e.TxHash == t.TxHash)))
            {
                _logger.LogInformation($"New trade entry on chain {trade.ChainId} for tx {trade.TxHash}");
            }
            repository.Trades.Add(false, trades.Where(t => !existingTrades.Any(e => e.ChainId == t.ChainId && e.TxHash == t.TxHash)).ToArray());
            repository.Commit();
            unprocessed.Clear();
        }

        private async Task<IEnumerable<TradingPlattform>> StoreNewPlattforms(List<TradingPlattform> existingPlattforms, List<TradingPlattform> foundPlattforms)
        {
            var newPlattforms = foundPlattforms.Where(p => !existingPlattforms.Any(e => e.Router.Equals(p.Router, StringComparison.OrdinalIgnoreCase) && e.ChainId == p.ChainId));
            var addedPlattforms = new List<TradingPlattform>();
            if (newPlattforms.Any())
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IWeb3UnitOfWork>();
                foreach (var plattform in newPlattforms)
                {
                    try
                    {
                        plattform.Factory = await GetFactoryFromRouter(plattform.Router, plattform.ChainId);
                        if (plattform.Factory == null)
                        {
                            continue;
                        }
                        //Check if every required function is available
                        var web3 = _web3Service.GetWeb3InstanceOf(plattform.ChainId);
                        var chainInfo = _web3Service.GetBlockchainInfoOf(plattform.ChainId);
                        try
                        {
                            var isV2 = web3.Eth.GetContractQueryHandler<GetPairOfFunction>().QueryAsync<GetPairOfFunctionOutputDTOBase>(plattform.Factory, new GetPairOfFunction()
                            {
                                TokenA = chainInfo.NativeCurrency.Address,
                                TokenB = chainInfo.StableCoin.Address
                            });

                            plattform.Version = PlattformVersion.V2;
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                var isV3 = web3.Eth.GetContractQueryHandler<GetPairV3OfFunction>().QueryAsync<GetPairOfFunctionOutputDTOBase>(plattform.Factory, new GetPairV3OfFunction()
                                {
                                    TokenA = chainInfo.NativeCurrency.Address,
                                    TokenB = chainInfo.StableCoin.Address,
                                    Fee = 500
                                });
                                plattform.Version = PlattformVersion.V3;
                            }
                            catch (Exception exV3)
                            {
                                continue;
                            }
                        }

                        addedPlattforms.Add(plattform);
                    }
                    catch (Exception ex)
                    {

                    }
                }
                repository.TradingPlattforms.Add(true, addedPlattforms.ToArray());
                repository.Commit();
            }

            return addedPlattforms;
        }

        private void RetrieveTradeInformation(TransactionReceipt receipt, string from, string to, ref EventLog<TransferEventDTO> fromLog, ref EventLog<TransferEventDTO> toLog)
        {
            var events = receipt.DecodeAllEvents<TransferEventDTO>();
            foreach (var log in events)
            {
                if (log.Event != null)
                {
                    _logger.LogInformation($"{log.Log.Address} sent from {log.Event.From} to {log.Event.To}");
                    if (log.Event.From.Equals(from, StringComparison.OrdinalIgnoreCase)) //Sent Tokens
                    {
                        fromLog = log;
                        _logger.LogInformation($"Identified token transfer event from sender into contract");
                    }
                    else if (fromLog == null && log.Event.From.Equals(to, StringComparison.OrdinalIgnoreCase)) //Sent BNB if no Token <> Token Trade
                    {
                        fromLog = log;
                        _logger.LogInformation($"Identified BNB token transfer event from sender into contract");

                    }
                    else if (log.Event.To.Equals(from, StringComparison.OrdinalIgnoreCase)) //Received Tokens
                    {
                        toLog = log;
                        _logger.LogInformation($"Identified token transfer event from contract to sender");
                    }
                    else if (toLog == null && log.Event.To.Equals(to, StringComparison.OrdinalIgnoreCase)) //Receive BNB if no Token <> Token Trade
                    {
                        toLog = log;
                        _logger.LogInformation($"Identified BNB token transfer event from contract to sender");
                    }
                }
            }
        }

        private async Task<Trade?> ProcessTradeInformation(UnprocessesSwapEvent unprocessedEvent, TransactionReceipt receipt, string from, string to, EventLog<TransferEventDTO> fromLog, EventLog<TransferEventDTO> toLog, List<TradingPlattform> existingPlattforms, CancellationToken cancellationToken)
        {
            var chainInfo = _web3Service.GetBlockchainInfoOf(unprocessedEvent.chainId);
            var tokenInserted = fromLog!.Log.Address;
            var tokenReceived = toLog!.Log.Address;
            var tokenIn = await GetTokenData(tokenInserted, unprocessedEvent.chainId, cancellationToken);
            var tokenOut = await GetTokenData(tokenReceived, unprocessedEvent.chainId, cancellationToken);
            var plattform = existingPlattforms.FirstOrDefault(p => p.ChainId == unprocessedEvent.chainId && p.Router.Equals(to, StringComparison.OrdinalIgnoreCase));
            using (var scope = _scopeFactory.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IWeb3UnitOfWork>();
                var openTrades = repository.Trades.Where(t => (t.TokenOutId == tokenIn.Id
                && t.Trader == from
                && t.TokensSold < t.TokenOutAmount) || (t.TxHash == receipt.TransactionHash && t.ChainId == unprocessedEvent.chainId)).Sort(System.ComponentModel.ListSortDirection.Ascending, t => t.Created).GetAll();
                if (openTrades.Any(t => t.TxHash == receipt.TransactionHash))
                {
                    return null;
                }
                var newEntry = new Trade
                {
                    Trader = from,
                    PlattformId = plattform?.Id ?? null,
                    TokenInId = tokenIn.Id,
                    TokenOutId = tokenOut.Id,
                    TokenIn = tokenIn.Address,
                    TokenOut = tokenOut.Address,
                    TokenInPrice = tokenIn.Price,
                    TokenOutPrice = tokenOut.Price,
                    TokenInAmount = (double)UnitConversion.Convert.FromWei(fromLog.Event.Value, tokenIn.Decimals),
                    TokenOutAmount = (double)UnitConversion.Convert.FromWei(toLog.Event.Value, tokenOut.Decimals),
                    ChainId = unprocessedEvent.chainId,
                    TxHash = receipt.TransactionHash
                };

                if (openTrades.Any())
                {
                    var tokenInAmount = newEntry.TokenInAmount;
                    foreach (var openTrade in openTrades)
                    {
                        var finished = CalculateProfitAndShouldFinish(newEntry, openTrade, ref tokenInAmount);
                        repository.Trades.Update(openTrade);
                        if (finished)
                        {
                            break;
                        }
                    }
                    repository.Trades.Add(newEntry);
                    repository.Commit();
                    return null;
                }

                return newEntry;
            }
        }

        public static bool CalculateProfitAndShouldFinish(Trade newTrade, Trade openTrade, ref double tokenInAmount)
        {
            var finished = false;
            if (openTrade.TokenOutPrice <= 0)
            {
                return false;
            }
            var remainingTokensToSell = openTrade.TokenOutAmount - openTrade.TokensSold;
            double averagePrice = 0;
            if (remainingTokensToSell > tokenInAmount)
            {
                var averagePriceMult = openTrade.TokensSold * openTrade.AverageSellPrice;
                var averagePriceNow = remainingTokensToSell * newTrade.TokenInPrice;
                averagePrice = (averagePriceMult + averagePriceNow) / (openTrade.TokensSold + remainingTokensToSell);


                openTrade.TokensSold += remainingTokensToSell;

            }
            else
            {
                tokenInAmount -= remainingTokensToSell;
                //Calculate Average Price
                var averagePriceMult = openTrade.TokensSold * openTrade.AverageSellPrice;
                var averagePriceNow = remainingTokensToSell * newTrade.TokenInPrice;
                averagePrice = (averagePriceMult + averagePriceNow) / (openTrade.TokensSold + remainingTokensToSell);
                openTrade.TokensSold = openTrade.TokenOutAmount;
                finished = true;
            }
            openTrade.AverageSellPrice = averagePrice;
            openTrade.Profit = openTrade.AverageSellPrice / openTrade.TokenOutPrice * 100 - 100;
            return finished;
        }

        private async Task<string> GetFactoryFromRouter(string to, int chainId)
        {
            var web3 = _web3Service.GetWeb3InstanceOf(chainId);
            var factory = await web3.Eth.GetContract(RouterV2.ABI, to).GetFunction("factory").CallAsync<string>();
            return factory;
        }

        //BNB Trade: https://bscscan.com/tx/0x596b80b249a296040d4b48e03ee0cd7396840ca6b45e56ccbccf273b645f41d1#eventlog
        //Other token trade: https://bscscan.com/tx/0x958aae7d28396ce850e12ece6ca5cc8ac882350002344b083e374052e6508c2f#eventlog

        private async Task<Token> GetTokenDataFromDatabase(string address, int chainId)
        {
            return null;
        }

        private async Task<Token> GetTokenData(string address, int chainId, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            return await mediator.Send(new AddTokenCommand
            {
                Address = address,
                ChainId = chainId,
            });
        }


        private async Task<TransactionReceipt> GetTransactionReceipt(UnprocessesSwapEvent swapEvent)
        {
            var txHash = swapEvent.log.TransactionHash;
            try
            {
                var web3 = _web3Service.GetWeb3InstanceOf(swapEvent.chainId);
                var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
                _logger.LogInformation($"Got transaction receipt for: {txHash}");
                return receipt;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error receiving transaction receipt for: {txHash}");
                return null;
            }
        }

        private async Task<Dictionary<UnprocessesSwapEvent, RpcRequestResponseBatchItem<EthGetTransactionReceipt, TransactionReceipt>>> GetTransactionReceipts(List<UnprocessesSwapEvent> swapEvents)
        {
            var dict = new Dictionary<UnprocessesSwapEvent, RpcRequestResponseBatchItem<EthGetTransactionReceipt, TransactionReceipt>>();
            var requests = new Dictionary<Nethereum.Web3.Web3, RpcRequestResponseBatch>();

            foreach (var swapEvent in swapEvents)
            {
                if (dict.ContainsKey(swapEvent))
                {
                    continue;
                }
                var web3 = _web3Service.GetWeb3InstanceOf(swapEvent.chainId);
                RpcRequestResponseBatch request;
                if (!requests.ContainsKey(web3))
                {
                    requests.Add(web3, request = new RpcRequestResponseBatch());
                }
                else
                {
                    request = requests[web3];
                }
                var batchItem = new RpcRequestResponseBatchItem<EthGetTransactionReceipt, TransactionReceipt>((EthGetTransactionReceipt)web3.Eth.Transactions.GetTransactionReceipt, web3.Eth.Transactions.GetTransactionReceipt.BuildRequest(swapEvent.log.TransactionHash));
                request.BatchItems.Add(batchItem);

                dict.Add(swapEvent, batchItem);
            }
            foreach (var entry in requests)
            {
                await entry.Key.Client.SendBatchRequestAsync(entry.Value);
            }
            return dict;
        }

        private async Task<(StreamingWebSocketClient, EthLogsObservableSubscription)> GetLogsTokenTransfer_Observable_Subscription(int chainId)
        {
            // ** SEE THE TransferEventDTO class below **
            var blockchainInfo = _web3Service.GetBlockchainInfoOf(chainId);
            var client = new StreamingWebSocketClient(blockchainInfo.WssUrl);

            var filterTransfers = Event<SwapEventV2>.GetEventABI().CreateFilterInput();

            var subscription = new EthLogsObservableSubscription(client);
            client.Error += (sender, ex) =>
            {
                _logger.LogError($"Error in Websocket Connection: {ex.Message}");
                client.StopAsync().Wait();
                subscription.UnsubscribeAsync().Wait();
                client.StartAsync().Wait();
                subscription.SubscribeAsync(filterTransfers).Wait();
            };
            subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(log =>
            {
                try
                {
                    //decode the log into a typed event log
                    //if (!_watchlist.Any(w => w.Address.Equals(log.Address, StringComparison.OrdinalIgnoreCase)))
                    //{
                    //    return;
                    //}
                    _unprocessedSwapEvents.Enqueue(new UnprocessesSwapEvent(chainId, log));
                    var decoded = Event<SwapEventV2>.DecodeEvent(log);
                    if (decoded != null)
                    {
                        _logger.LogInformation($"Chain {chainId}: Contract address: " + log.Address + " Log Transfer from:" + decoded.Event.From);
                    }
                    else
                    {
                        // the log may be an event which does not match the event
                        // the name of the function may be the same
                        // but the indexed event parameters may differ which prevents decoding
                        _logger.LogWarning($"Chain {chainId}:Found not standard swap log {log.Address}: {log.TransactionHash}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Chain {chainId}: Log Address: {log.Address}: {log.TransactionHash} is not a standard transfer log: {ex.Message}");
                }
            });

            // open the web socket connection
            await client.StartAsync();
            // begin receiving subscription data
            // data will be received on a background thread
            await subscription.SubscribeAsync(filterTransfers);

            //// run for a while
            //await Task.Delay(TimeSpan.FromMinutes(60));

            return (client, subscription);
        }
    }
}
