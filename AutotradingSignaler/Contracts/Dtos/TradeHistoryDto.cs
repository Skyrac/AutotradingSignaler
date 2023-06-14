namespace AutotradingSignaler.Contracts.Dtos
{
    public class TradeHistoryDto
    {
        public List<TradeDto> Trades { get; set; }
        public int CurrentOffset { get; set; } = 0;
        public int Total { get; set; }
    }
}
