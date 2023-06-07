using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace AutotradingSignaler.Contracts.Web3.Events;

[Event("Swap")]
public class SwapEventV2 : IEventDTO
{
    [Parameter("address", "sender", 1, true)]
    public virtual string From { get; set; }

    [Parameter("uint256", "amount0In", 2, false)]
    public virtual BigInteger Amount0In { get; set; }

    [Parameter("uint256", "amount1In", 3, false)]
    public virtual BigInteger Amount1In { get; set; }

    [Parameter("uint256", "amount0Out", 4, false)]
    public virtual BigInteger Amount0Out { get; set; }

    [Parameter("uint256", "amount1Out", 5, false)]
    public virtual BigInteger Amount1Out { get; set; }

    [Parameter("address", "to", 6, true)]
    public virtual string To { get; set; }
}
