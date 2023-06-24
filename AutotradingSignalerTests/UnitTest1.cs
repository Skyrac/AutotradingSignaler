using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Core.Web.Background;

namespace AutotradingSignalerTests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var newTrade = new Trade()
            {
                TokenIn = "0x93fd0bd04556d78820d76dfe3873e8c44f9d6e0d",
                TokenOut = "",
                TokenInAmount = 190394866147,
                TokenOutAmount = 0.0327526200426057,
                Profit = 0,
                ChainId = 56,
                TokenInPrice = 1415089740425055.8
            };
            //WalletTransferBackgroundSync.CalculateProfitAndShouldFinish()
        }
    }
}