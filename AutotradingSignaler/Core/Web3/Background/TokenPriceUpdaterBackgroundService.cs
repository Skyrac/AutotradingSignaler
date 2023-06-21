using _1InchApi;
using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Contracts.Web3;
using AutotradingSignaler.Contracts.Web3.Contracts;
using AutotradingSignaler.Core.Web;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Rationals;

namespace AutotradingSignaler.Core.Web3.Background;

public class TokenPriceUpdaterBackgroundService : BackgroundService
{
    private readonly ILogger<TokenPriceUpdaterBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Web3Service _web3Service;
    public record ReserveCall(
        MulticallInputOutput<GetReserveOfFunction, GetReserveOfFunctionOutputDTOBase> reserve,
        MulticallInputOutput<GetToken0OfFunction, GetTokenOfFunctionOutputDTOBase> token0,
        MulticallInputOutput<GetToken1OfFunction, GetTokenOfFunctionOutputDTOBase> token1
        );
    public record TokenPrice(
        double price,
        double liquidity
    );
    public TokenPriceUpdaterBackgroundService(ILogger<TokenPriceUpdaterBackgroundService> logger, IServiceScopeFactory scopeFactory, Web3Service web3Service)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _web3Service = web3Service;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(5000);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                //var callist = new List<IMulticallInputOutput>();
                foreach (var web3Instance in _web3Service.GetWeb3Instances())
                {
                    //1. Get $ Price of BNB from OneInch
                    var chainInfo = _web3Service.GetBlockchainInfoOf(web3Instance.Key);

                    if (chainInfo.StableCoin == null)
                    {
                        continue;
                    }
                    var price = await OneInchApiWrapper.GetQuote((Chain)web3Instance.Key, chainInfo.NativeCurrency.Address, chainInfo.StableCoin.Address!, 1, chainInfo.NativeCurrency.Decimals);
                    var nativeTokenPrice = double.Parse(price.toTokenAmount) / Math.Pow(10, chainInfo.NativeCurrency.Decimals);
                    var tradingPlattformList = new List<TradingPlattform>();
                    var tokens = GetTokens(web3Instance.Key);
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var _unitOfWork = scope.ServiceProvider.GetRequiredService<IWeb3UnitOfWork>();
                        tradingPlattformList = _unitOfWork.TradingPlattforms.Where(tp => tp.ChainId == chainInfo.ChainId && tp.IsValid).GetAll().ToList();
                    }
                    //get pairs
                    var pairDict = await GetPairs(tokens, web3Instance.Value, chainInfo.ChainId, chainInfo.NativeCurrency.Address, tradingPlattformList);
                    var reservesDict = await GetReserves(pairDict, web3Instance.Value);
                    Dictionary<Token, List<TokenPrice>> priceDict = CalculateBestPrice(chainInfo, nativeTokenPrice, reservesDict);
                    var nativeToken = tokens.FirstOrDefault(t => t.Address == "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
                    var wrappedNativeToken = tokens.FirstOrDefault(t => t.Address.Equals(chainInfo.NativeCurrency.Address, StringComparison.OrdinalIgnoreCase));
                    nativeToken.Price = nativeTokenPrice;
                    wrappedNativeToken.Price = nativeTokenPrice;
                    priceDict.Add(nativeToken, new List<TokenPrice>());
                    priceDict.Add(wrappedNativeToken, new List<TokenPrice>());
                    StoreTokenPrices(priceDict.Keys);
                    await Task.Delay(60000 * 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.InnerException}");
            }
        }
    }

    public static Dictionary<Token, List<TokenPrice>> CalculateBestPrice(BlockchainDto chainInfo, double nativeTokenPrice, Dictionary<Token, List<ReserveCall>> reservesDict)
    {
        var priceDict = new Dictionary<Token, List<TokenPrice>>();
        foreach (var token in reservesDict.Keys)
        {
            priceDict.Add(token, new List<TokenPrice>());
            foreach (var reserveCall in reservesDict[token])
            {
                var tokenPriceResult = GetTokenPrice(token, chainInfo.NativeCurrency, reserveCall, nativeTokenPrice);
                if (tokenPriceResult.price == 0 || tokenPriceResult.liquidity == 0)
                {
                    continue;
                }
                priceDict[token].Add(tokenPriceResult);
                //_logger.LogInformation("Price Update for Token: {0} {1} with Total Liquidity {2}", token.Name, tokenPriceResult.price, tokenPriceResult.liquidity);
            }

            token.Price = priceDict[token].MaxBy(t => t.liquidity)?.price ?? 0;
        }

        return priceDict;
    }

    public static TokenPrice GetTokenPrice(Token token, BlockchainCurrency nativeToken, ReserveCall reserve, double nativeTokenPrice)
    {
        //    (uint256 res0, uint256 res1,) = pair.getReserves();
        //    if (res0 == 0 && res1 == 0)
        //    {
        //        return 0;
        //    }
        //    ERC20 tokenB = address(pair.token0()) == address(coin) ? ERC20(pair.token1()) : ERC20(pair.token1());
        //    uint256 mainRes = address(pair.token0()) == address(coin) ? res1 : res0;
        //    uint256 secondaryRes = mainRes == res0 ? res1 : res0;
        //    return (mainRes * (10 * *tokenB.decimals())) / secondaryRes;
        var tokenBDecimals = nativeToken.Decimals;
        var tokenB = new Rational(token.Address.Equals(reserve.token0.Output?.TokenAddress, StringComparison.OrdinalIgnoreCase) ?
                                reserve.reserve.Output.Reserve1
                                : reserve.reserve.Output.Reserve0);

        var tokenA = new Rational(tokenB == reserve.reserve.Output.Reserve1 ?
                            reserve.reserve.Output.Reserve0
                            : reserve.reserve.Output.Reserve1);
        var amountA = (double)(tokenA / Rational.Pow(10, token.Decimals == 0 ? 18 : token.Decimals));
        var amountB = (double)(tokenB / Rational.Pow(10, tokenBDecimals));
        if (amountA < 3 || amountB < 3)
        {
            return new TokenPrice(0, 0);
        }
        var price = (double)(tokenB / Rational.Pow(10, nativeToken.Decimals)) / (double)(tokenA / Rational.Pow(10, token.Decimals)) * nativeTokenPrice; //nativeToken per Token
        var liquidity = (double)(tokenB / Rational.Pow(10, nativeToken.Decimals)) * nativeTokenPrice + (double)(tokenA / Rational.Pow(10, token.Decimals == 0 ? 18 : token.Decimals)) * price;
        return new TokenPrice(price, liquidity);
    }

    //function getCoinAmount(address _pair, address _coinOfInterest, uint256 _amount) public view returns(uint256)
    //{
    //    IUniswapV2Pair pair = IUniswapV2Pair(_pair);
    //    if (address(pair) == address(0))
    //    {
    //        return 0;
    //    }
    //    bool coin1IsOfInterest = pair.token0() == _coinOfInterest;
    //    bool coin2IsOfInterest = pair.token1() == _coinOfInterest;
    //    (uint256 res0, uint256 res1,) = pair.getReserves();
    //    if ((res0 == 0 && res1 == 0) || (!coin1IsOfInterest && !coin2IsOfInterest))
    //    {
    //        return 0;
    //    }
    //    uint256 totalSupply = pair.totalSupply();
    //    return _amount.mul(coin1IsOfInterest ? res0 : res1).div(totalSupply);
    //}

    ///* =====================================================================================================================
    //                                                Utility Functions
    //===================================================================================================================== */
    //function getTokenPrice() public returns(uint256)
    //{
    //    address coinLpAddress = coin.liquidityPair();
    //    if (coinLpAddress != lpAddress)
    //    {
    //        lpAddress = coinLpAddress;
    //        hourlyIndex = 0;
    //        lastAveragePrice = 0;
    //        previousAveragePrice = 0;
    //    }
    //    IUniswapV2Pair pair = IUniswapV2Pair(lpAddress);
    //    (uint256 res0, uint256 res1,) = pair.getReserves();
    //    if (res0 == 0 && res1 == 0)
    //    {
    //        return 0;
    //    }
    //    ERC20 tokenB = address(pair.token0()) == address(coin) ? ERC20(pair.token1()) : ERC20(pair.token1());
    //    uint256 mainRes = address(pair.token0()) == address(coin) ? res1 : res0;
    //    uint256 secondaryRes = mainRes == res0 ? res1 : res0;
    //    return (mainRes * (10 * *tokenB.decimals())) / secondaryRes;
    //}



    public static async Task<Dictionary<Token, List<ReserveCall>>> GetReserves(Dictionary<Token, List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>>? pairDict, Nethereum.Web3.Web3 web3)
    {
        var reservesDict = new Dictionary<Token, List<ReserveCall>>();
        var callist = new List<IMulticallInputOutput>();

        foreach (var entry in pairDict.Keys)
        {
            reservesDict.Add(entry, new List<ReserveCall>());
            foreach (var pairResult in pairDict[entry])
            {
                var pair = pairResult.Output?.Pair;
                if (string.IsNullOrEmpty(pair) || pair == "0x0000000000000000000000000000000000000000") continue;
                var message = new GetReserveOfFunction();
                var call1 = new MulticallInputOutput<GetReserveOfFunction, GetReserveOfFunctionOutputDTOBase>(message, pair);
                var call2 = new MulticallInputOutput<GetToken0OfFunction, GetTokenOfFunctionOutputDTOBase>(new GetToken0OfFunction(), pair);
                var call3 = new MulticallInputOutput<GetToken1OfFunction, GetTokenOfFunctionOutputDTOBase>(new GetToken1OfFunction(), pair);
                reservesDict[entry].Add(new ReserveCall(call1, call2, call3));
                callist.Add(call1);
                callist.Add(call2);
                callist.Add(call3);
            }
        }
        await web3.Eth.GetMultiQueryHandler().MultiCallAsync(500, callist.ToArray()).ConfigureAwait(false);

        return reservesDict;
    }

    public static async Task<Dictionary<Token, List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>>?> GetPairs(IList<Token> tokens, Nethereum.Web3.Web3 web3, int chainId, string chainNativeTokenAddress, IList<TradingPlattform> tradingPlattformList)
    {
        var pairDict = new Dictionary<Token, List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>>();
        var finalizedDict = new Dictionary<Token, List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>>();
        var callist = new List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>();

        tradingPlattformList.Add(new TradingPlattform()
        {
            ChainId = chainId,
            Factory = "0xcA143Ce32Fe78f1f7019d7d551a6402fC5350c73"
        });
        if (tradingPlattformList == null)
        {
            return null;
        }
        foreach (var plattform in tradingPlattformList)
        {
            foreach (var token in tokens)
            {
                if (token.Address == "0x0000000000000000000000000000000000000000" || token.Address == "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee" || token.Address == chainNativeTokenAddress) continue;
                if (!pairDict.ContainsKey(token))
                {
                    pairDict.Add(token, new List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>());
                }

                //TODO: If we want to store the liqudity and price of a token on a given trading plattform
                var message = new GetPairOfFunction()
                {
                    TokenA = token.Address,
                    TokenB = chainNativeTokenAddress
                };
                var call = new MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>(message, plattform.Factory);
                pairDict[token].Add(call);
                callist.Add(call);
            }
            try
            {
                await web3.Eth.GetMultiQueryHandler().MultiCallAsync(500, callist.ToArray()).ConfigureAwait(false);
                foreach (var entry in pairDict)
                {
                    if (!finalizedDict.ContainsKey(entry.Key))
                    {
                        finalizedDict.Add(entry.Key, entry.Value);
                    }
                    else
                    {
                        finalizedDict[entry.Key].AddRange(entry.Value);
                    }
                }
                pairDict.Clear();

            }
            catch (Exception ex)
            {
                //Failure on Plattform

                pairDict.Clear();
                callist.Clear();
            }
        }
        return finalizedDict;
    }



    private IList<Token> GetTokens(int chain)
    {
        using var scope = _scopeFactory.CreateScope();
        var _unitOfWork = scope.ServiceProvider.GetRequiredService<IWeb3UnitOfWork>();
        return _unitOfWork.Tokens.Where(t => t.ChainId == chain).GetAll().ToList();
    }


    private void StoreTokenPrices(ICollection<Token> tokens)
    {
        using var scope = _scopeFactory.CreateScope();
        var _unitOfWork = scope.ServiceProvider.GetRequiredService<IWeb3UnitOfWork>();
        foreach (var token in tokens)
        {
            _unitOfWork.Tokens.Update(token);
        }
        //_unitOfWork.Tokens.Update(tokens.ToArray());
        _unitOfWork.Commit();
    }
}
