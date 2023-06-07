using Nethereum.Contracts.Standards.ERC20.TokenList;

namespace AutotradingSignaler.Contracts.Dtos
{
    public class TokenDto : Token
    {
        public decimal Balance { get; set; }
    }
}
