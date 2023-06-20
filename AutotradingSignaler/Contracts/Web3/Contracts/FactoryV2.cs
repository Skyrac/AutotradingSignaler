using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace AutotradingSignaler.Contracts.Web3.Contracts;

[Function("getPair", "address")]
public class GetPairOfFunction : FunctionMessage
{
    [Parameter("address", "_tokenA", 1)]
    public virtual string TokenA { get; set; }
    [Parameter("address", "_tokenB", 2)]
    public virtual string TokenB { get; set; }
}

[FunctionOutput]
public class GetPairOfFunctionOutputDTOBase : IFunctionOutputDTO
{
    [Parameter("address", "_pair", 1)]
    public virtual string Pair { get; set; }
}
