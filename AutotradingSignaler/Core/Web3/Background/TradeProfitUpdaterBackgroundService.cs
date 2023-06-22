namespace AutotradingSignaler.Core.Web3.Background
{
    public class TradeProfitUpdaterBackgroundService : BackgroundService
    {
        private readonly ILogger<TradeProfitUpdaterBackgroundService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public TradeProfitUpdaterBackgroundService(ILogger<TradeProfitUpdaterBackgroundService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    //1. Get all Open Trades and Update Profit based on sold tokens
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception in BackgroundService: {nameof(TradeProfitUpdaterBackgroundService)} - {ex?.InnerException?.Message ?? ex?.Message}");

                }

            }
        }
    }
}
