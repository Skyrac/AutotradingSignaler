namespace AutotradingSignaler.Contracts.Dtos
{
    public class WalletDto
    {
        public string Address { get; set; }
        public List<TokenDto> Tokens { get; set; } = new List<TokenDto>();
    }
}
