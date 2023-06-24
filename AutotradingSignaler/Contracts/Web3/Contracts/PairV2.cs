using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace AutotradingSignaler.Contracts.Web3.Contracts;

[Function("getReserves", typeof(GetReserveOfFunctionOutputDTOBase))]
public class GetReserveOfFunction : FunctionMessage
{

}

[FunctionOutput]
public class GetReserveOfFunctionOutputDTOBase : IFunctionOutputDTO
{

    [Parameter("uint112", "_reserve0", 1)]
    public virtual BigInteger Reserve0 { get; set; }
    [Parameter("uint112", "_reserve1", 1)]
    public virtual BigInteger Reserve1 { get; set; }
}

[Function("token0", "address")]
public class GetToken0OfFunction : FunctionMessage
{

}

[Function("token1", "address")]
public class GetToken1OfFunction : FunctionMessage
{

}

[FunctionOutput]
public class GetTokenOfFunctionOutputDTOBase : IFunctionOutputDTO
{

    [Parameter("address", "", 1)]
    public virtual string TokenAddress { get; set; }
}

[Function("totalSupply", "uint256")]
public class GetTotalSupplyOfFunction : FunctionMessage
{

}

[FunctionOutput]
public class GetTotalSupplyOfFunctionOutputDTOBase : IFunctionOutputDTO
{

    [Parameter("uint256", "", 1)]
    public virtual string TotalSupply { get; set; }
}

[Function("slot0")]
public class GetSlot0OfFunction : FunctionMessage
{

}

[FunctionOutput]
public class GetSlot0OfFunctionOutputDTOBase : IFunctionOutputDTO
{

    [Parameter("uint256", "", 1)]
    public virtual BigInteger SqrtPrice { get; set; }
}


[Function("liquidity")]
public class GetLiquidityOfFunction : FunctionMessage
{

}

[FunctionOutput]
public class GetLiquidityOfFunctionOutputDTOBase : IFunctionOutputDTO
{

    [Parameter("uint128", "", 1)]
    public virtual BigInteger Liquidity { get; set; }
}


