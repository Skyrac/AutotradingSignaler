using AutotradingSignaler.Contracts.Data;
using Mapster;

namespace AutotradingSignaler.Contracts.Dtos
{
    [AdaptFrom(typeof(Trade))]
    public class TradeDto
    {
        public string Trader { get; set; }
        public TradingPlattformDto Plattform { get; set; }
        public string TokenIn { get; set; }
        public string TokenOut { get; set; }
        public decimal TokenInAmount { get; set; }
        public decimal TokenOutAmount { get; set; }
        public int ChainId { get; set; }
        public string TxHash { get; set; }
        public double Profit { get; set; }
        public bool IsBuy { get; set; }
        public decimal TokensSold { get; set; }
        public double TokenInPrice { get; set; }
        public double TokenOutPrice { get; set; }
        public TokenDto TokenInData { get; set; }
        public TokenDto TokenOutData { get; set; }

        [AdaptMember(nameof(Trade.Created))]
        public DateTime Timestamp { get; set; }
    }
}
