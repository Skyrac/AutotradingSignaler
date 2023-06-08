using Mapster;
using Nethereum.Contracts.Standards.ERC20.TokenList;

namespace AutotradingSignaler.Contracts.Dtos
{
    [AdaptFrom(typeof(Data.Token))]
    public class TokenDto : Token
    {
        public decimal Balance { get; set; }
    }
}
