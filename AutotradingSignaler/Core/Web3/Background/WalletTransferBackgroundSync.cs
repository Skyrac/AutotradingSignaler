﻿using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Contracts.Web3.Events;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Util;
using System.Collections.Concurrent;
using System.Numerics;

namespace AutotradingSignaler.Core.Web.Background
{
    public class WalletTransferBackgroundSync : BackgroundService
    {
        private readonly ILogger<WalletTransferBackgroundSync> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Web3Service _web3Service;
        private readonly ConcurrentQueue<UnprocessesSwapEvent> _unprocessedSwapEvents = new ConcurrentQueue<UnprocessesSwapEvent>();
        private readonly List<string> _watchlist = new List<string>();

        private record UnprocessesSwapEvent(int chainId, FilterLog log);

        public WalletTransferBackgroundSync(ILogger<WalletTransferBackgroundSync> logger, Web3Service web3Service, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _web3Service = web3Service;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {

            var subs = new List<EthLogsObservableSubscription>();
            var counter = 0;

            while (!cancellationToken.IsCancellationRequested)
            {

                await Task.Delay(5000);
                try
                {
                    if (!subs.Any())
                    {
                        foreach (var chain in _web3Service.GetWeb3Instances())
                        {
                            subs.Add(await GetLogsTokenTransfer_Observable_Subscription(chain.Key));
                        }
                    }
                    //TODO: Check for new findings and execute process like notification  or trade execution

                    while (_unprocessedSwapEvents.TryDequeue(out var unprocessedEvent))
                    {
                        var receipt = await GetTransactionReceipt(unprocessedEvent);
                        if (receipt == null)
                        {
                            _logger.LogWarning($"{unprocessedEvent.chainId}: No receipt for {unprocessedEvent.log.TransactionHash}");
                            continue;
                        }
                        var from = receipt.From;
                        var to = receipt.To;
                        EventLog<TransferEventDTO> fromLog = null;  //Log of Token that was sent into swap
                        EventLog<TransferEventDTO> toLog = null;    //Log of Token that was received from swap
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
                                else if (to.Equals(log.Event.From, StringComparison.OrdinalIgnoreCase) || from.Equals(log.Event.To, StringComparison.OrdinalIgnoreCase))
                                {
                                    _logger.LogInformation($"Strange things did happen here");

                                }
                            }
                        }

                        if (fromLog != null && toLog != null)
                        {
                            var tokenInserted = fromLog!.Log.Address;
                            var tokenReceived = toLog!.Log.Address;
                            //1 try to get token data from db
                            //2 if not found get it from blockchain
                            var tokenIn = await GetTokenDataFromDatabase(tokenInserted, unprocessedEvent.chainId);
                            if (tokenIn == null)
                            {
                                tokenIn = GetTokenData(tokenInserted, unprocessedEvent.chainId, cancellationToken);
                            }
                            var tokenOut = await GetTokenDataFromDatabase(tokenReceived, unprocessedEvent.chainId);
                            if (tokenOut == null)
                            {
                                tokenOut = GetTokenData(tokenReceived, unprocessedEvent.chainId, cancellationToken);
                            }
                            var newEntry = new Trade
                            {
                                Trader = from,
                                Plattform = to,
                                TokenIn = tokenIn.Address,
                                TokenOut = tokenOut.Address,
                                TokenInAmount = UnitConversion.Convert.FromWei(fromLog.Event.Value, tokenIn.Decimals),
                                TokenOutAmount = UnitConversion.Convert.FromWei(toLog.Event.Value, tokenOut.Decimals),
                                ChainId = unprocessedEvent.chainId,
                                TxHash = receipt.TransactionHash
                            };
                            _logger.LogInformation($"New trade entry on chain {newEntry.ChainId} for tx {newEntry.TxHash}");
                        }
                        Thread.Sleep(500);
                    }
                    if (counter > 100)
                    {
                        counter = 0;
                        //TODO: Get all current watched addresses
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception in BackgroundService: {nameof(WalletTransferBackgroundSync)} - {ex?.InnerException?.Message ?? ex?.Message}");
                    subs.ForEach(async sub => await sub.UnsubscribeAsync());
                    await Task.Delay(TimeSpan.FromSeconds(20));
                    subs.Clear();
                }
                counter++;
            }
        }
        //BNB Trade: https://bscscan.com/tx/0x596b80b249a296040d4b48e03ee0cd7396840ca6b45e56ccbccf273b645f41d1#eventlog
        //Other token trade: https://bscscan.com/tx/0x958aae7d28396ce850e12ece6ca5cc8ac882350002344b083e374052e6508c2f#eventlog

        private async Task<Token> GetTokenDataFromDatabase(string address, int chainId)
        {
            return null;
        }

        private Token GetTokenData(string address, int chainId, CancellationToken cancellationToken)
        {
            var web3 = _web3Service.GetWeb3InstanceOf(chainId);
            var tasks = new List<Task>();
            var decimals = web3.Eth.ERC20.GetContractService(address).DecimalsQueryAsync();
            tasks.Add(decimals);
            var name = web3.Eth.ERC20.GetContractService(address).NameQueryAsync();
            tasks.Add(name);
            var symbol = web3.Eth.ERC20.GetContractService(address).SymbolQueryAsync();
            tasks.Add(symbol);
            Task.WaitAll(tasks.ToArray(), cancellationToken);
            return new Token
            {
                Address = address,
                ChainId = chainId,
                Name = name.Result,
                Symbol = symbol.Result,
                Decimals = decimals.Result,
            };
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

        private async Task<EthLogsObservableSubscription> GetLogsTokenTransfer_Observable_Subscription(int chainId)
        {
            // ** SEE THE TransferEventDTO class below **
            var blockchainInfo = _web3Service.GetBlockchainInfoOf(chainId);
            var client = new StreamingWebSocketClient(blockchainInfo.WssUrl);

            var filterTransfers = Event<SwapEventV2>.GetEventABI().CreateFilterInput();

            var subscription = new EthLogsObservableSubscription(client);
            subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(log =>
            {
                try
                {
                    // decode the log into a typed event log
                    //if (!_watchlist.Any(w => w.Equals(log.Address, StringComparison.OrdinalIgnoreCase)))
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
            // await client.StartAsync();

            // begin receiving subscription data
            // data will be received on a background thread
            // await subscription.SubscribeAsync(filterTransfers);

            //// run for a while
            //await Task.Delay(TimeSpan.FromMinutes(60));

            return subscription;
        }
    }
}
