namespace AutotradingSignaler.Contracts.Dtos
{
    public class WalletDto
    {
        public string Address { get; set; }
        public List<TokenDto> Tokens { get; set; } = new List<TokenDto>();
        public double ProfitPerformance { get; set; }
        public double MaxDrawdown { get; set; }
        public double Trades { get; set; }
    }
}
