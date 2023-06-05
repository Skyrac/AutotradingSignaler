using AutotradingSignaler.Contracts.Web3.Events;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System.Collections.Concurrent;

namespace AutotradingSignaler.Core.Web.Background
{
    public class WalletTransferBackgroundSync : BackgroundService
    {
        private readonly ILogger<WalletTransferBackgroundSync> _logger;
        private readonly Web3Service _web3Service;
        private readonly ConcurrentQueue<UnprocessesSwapEvent> _unprocessedSwapEvents = new ConcurrentQueue<UnprocessesSwapEvent>();
        private readonly List<string> _watchlist = new List<string>();

        private record UnprocessesSwapEvent(int chainId, FilterLog log);

        public WalletTransferBackgroundSync(ILogger<WalletTransferBackgroundSync> logger, Web3Service web3Service)
        {
            _logger = logger;
            _web3Service = web3Service;
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
                        foreach (var log in receipt.DecodeAllEvents<TransferEventDTO>())
                        {
                            if (log.Event != null)
                            {
                                _logger.LogInformation($"{log.Log.Address} sent from {log.Event.From} to {log.Event.To}");
                            }
                        }
                    }
                    if (counter > 100)
                    {

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
            }
        }
        //BNB Trade: https://bscscan.com/tx/0x596b80b249a296040d4b48e03ee0cd7396840ca6b45e56ccbccf273b645f41d1#eventlog
        //Other token trade: https://bscscan.com/tx/0x958aae7d28396ce850e12ece6ca5cc8ac882350002344b083e374052e6508c2f#eventlog
        //Get Transaction Log of txhash: 

        //        string transactionHash = "0xYourTransactionHash";

        //        var web3 = new Web3("https://mainnet.infura.io/v3/your-infura-project-id");

        //        var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);

        //if (receipt != null && receipt.Logs != null)
        //{
        //    foreach (var log in receipt.Logs)
        //    {
        //        // Access the log data and process it accordingly
        //        Console.WriteLine($"Log Address: {log.Address}");
        //        Console.WriteLine($"Log Topics: {string.Join(", ", log.Topics)}");
        //        Console.WriteLine($"Log Data: {log.Data}");
        //    }
        //}
        //else
        //{
        //    Console.WriteLine("No logs found for the transaction.");
        //}

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
            await client.StartAsync();

            // begin receiving subscription data
            // data will be received on a background thread
            await subscription.SubscribeAsync(filterTransfers);

            //// run for a while
            //await Task.Delay(TimeSpan.FromMinutes(60));

            return subscription;
        }
    }
}
