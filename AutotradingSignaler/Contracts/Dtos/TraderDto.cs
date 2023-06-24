namespace AutotradingSignaler.Contracts.Dtos
{
    public class TraderDto
    {
        public string Address { get; set; }
        public double Profit { get; set; }
        public int Trades { get; set; }
        public double MaxDrawdown { get; set; }
    }
}
