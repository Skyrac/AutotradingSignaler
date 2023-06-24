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
    public record BaseReserveCall(
        MulticallInputOutput<GetToken0OfFunction, GetTokenOfFunctionOutputDTOBase> token0,
        MulticallInputOutput<GetToken1OfFunction, GetTokenOfFunctionOutputDTOBase> token1
        );
    public record ReserveCall(
        MulticallInputOutput<GetReserveOfFunction, GetReserveOfFunctionOutputDTOBase> reserve,
        MulticallInputOutput<GetToken0OfFunction, GetTokenOfFunctionOutputDTOBase> token0,
        MulticallInputOutput<GetToken1OfFunction, GetTokenOfFunctionOutputDTOBase> token1
        ) : BaseReserveCall(token0, token1);
    public record ReserveV3Call(
        MulticallInputOutput<GetSlot0OfFunction, GetSlot0OfFunctionOutputDTOBase> sqrtPriceX96,
        MulticallInputOutput<GetLiquidityOfFunction, GetLiquidityOfFunctionOutputDTOBase> liquidity,
        MulticallInputOutput<GetToken0OfFunction, GetTokenOfFunctionOutputDTOBase> token0,
        MulticallInputOutput<GetToken1OfFunction, GetTokenOfFunctionOutputDTOBase> token1
        ) : BaseReserveCall(token0, token1);
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
                    var tradingPlattformList = new List<TradingPlattform>();
                    var tokens = GetTokens(web3Instance.Key);
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var _unitOfWork = scope.ServiceProvider.GetRequiredService<IWeb3UnitOfWork>();
                        tradingPlattformList = _unitOfWork.TradingPlattforms.Where(tp => tp.ChainId == chainInfo.ChainId && tp.IsValid).GetAll().ToList();
                    }
                    //get pairs

                    var tokenPrices = await ProcessTokensAndReceivePriceData(web3Instance.Value, chainInfo, tokens, tradingPlattformList);
                    StoreTokenPrices(tokenPrices);
                    await Task.Delay(60000 * 5);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.InnerException}");
            }
        }
    }

    public static async Task<List<Token>> ProcessTokensAndReceivePriceData(Nethereum.Web3.Web3 web3, BlockchainDto chainInfo, IList<Token> tokens, IList<TradingPlattform> tradingPlattforms)
    {
        if (!tokens.Any())
        {
            return new List<Token>();
        }
        var tokenPrices = new Dictionary<Token, List<TokenPrice>>();
        var price = await OneInchApiWrapper.GetQuote((Chain)chainInfo.ChainId, chainInfo.NativeCurrency.Address, chainInfo.StableCoin.Address!, 1, chainInfo.NativeCurrency.Decimals);
        var nativeTokenPrice = double.Parse(price.toTokenAmount) / Math.Pow(10, chainInfo.NativeCurrency.Decimals);
        var reservesDict = await GetPairs(tokens, web3, chainInfo.ChainId, chainInfo.NativeCurrency.Address, tradingPlattforms.DistinctBy(t => t.Factory).ToList());
        try
        {
            //v2
            foreach (var entry in CalculateBestPrice(chainInfo, nativeTokenPrice, reservesDict))
            {
                if (!tokenPrices.ContainsKey(entry.Key))
                {
                    tokenPrices.Add(entry.Key, new List<TokenPrice>());
                }
                tokenPrices[entry.Key].AddRange(entry.Value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        try
        {
            //v3
            foreach (var entry in CalculateBestPriceV3(chainInfo, nativeTokenPrice, reservesDict))
            {
                if (!tokenPrices.ContainsKey(entry.Key))
                {
                    tokenPrices.Add(entry.Key, new List<TokenPrice>());
                }
                tokenPrices[entry.Key].AddRange(entry.Value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }


        foreach (var token in tokenPrices.Keys)
        {
            token.Price = tokenPrices[token].MaxBy(t => t.liquidity)?.price ?? 0;
        }

        var nativeToken = tokens.FirstOrDefault(t => t.Address == "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
        var wrappedNativeToken = tokens.FirstOrDefault(t => t.Address.Equals(chainInfo.NativeCurrency.Address, StringComparison.OrdinalIgnoreCase));
        if (nativeToken != null)
        {
            nativeToken.Price = nativeTokenPrice;
            tokenPrices.Add(nativeToken, new List<TokenPrice>());
        }
        if (wrappedNativeToken != null)
        {
            wrappedNativeToken.Price = nativeTokenPrice;
            tokenPrices.Add(wrappedNativeToken, new List<TokenPrice>());
        }

        return tokenPrices.Keys.ToList();
    }

    public static Dictionary<Token, List<TokenPrice>> CalculateBestPrice(BlockchainDto chainInfo, double nativeTokenPrice, Dictionary<Token, List<BaseReserveCall>> reservesDict)
    {
        var priceDict = new Dictionary<Token, List<TokenPrice>>();

        foreach (var token in reservesDict.Keys)
        {
            try
            {
                priceDict.Add(token, new List<TokenPrice>());
                foreach (var entry in reservesDict[token])
                {
                    if (entry is not ReserveCall reserveCall) { continue; }
                    var tokenPriceResult = GetTokenPrice(token, chainInfo.NativeCurrency, reserveCall, nativeTokenPrice);
                    if (tokenPriceResult.price == 0 || tokenPriceResult.liquidity == 0)
                    {
                        continue;
                    }
                    priceDict[token].Add(tokenPriceResult);
                    //_logger.LogInformation("Price Update for Token: {0} {1} with Total Liquidity {2}", token.Name, tokenPriceResult.price, tokenPriceResult.liquidity);
                }

            }
            catch (Exception ex)
            {

            }
        }

        return priceDict;
    }

    public static Dictionary<Token, List<TokenPrice>> CalculateBestPriceV3(BlockchainDto chainInfo, double nativeTokenPrice, Dictionary<Token, List<BaseReserveCall>> reservesDict)
    {
        var priceDict = new Dictionary<Token, List<TokenPrice>>();
        foreach (var token in reservesDict.Keys)
        {
            priceDict.Add(token, new List<TokenPrice>());
            foreach (var entry in reservesDict[token])
            {
                if (entry is not ReserveV3Call reserveCall || !reserveCall.sqrtPriceX96.Success || !reserveCall.liquidity.Success || !reserveCall.token0.Success || !reserveCall.token1.Success) continue;
                var sqrtPrice = new Rational(reserveCall.sqrtPriceX96.Output.SqrtPrice) / Rational.Pow(2, 96);
                var pairPrice = Rational.Pow(sqrtPrice, 2);
                var tokenADecimals = token.Address.Equals(reserveCall.token0.Output?.TokenAddress, StringComparison.OrdinalIgnoreCase) ? token.Decimals : chainInfo.NativeCurrency.Decimals;
                var tokenBDecimals = token.Address.Equals(reserveCall.token0.Output?.TokenAddress, StringComparison.OrdinalIgnoreCase) ? chainInfo.NativeCurrency.Decimals : token.Decimals;
                var buyOneOfTokenA = (double)(pairPrice / (Rational.Pow(10, tokenBDecimals) / Rational.Pow(10, tokenADecimals)));
                var price = buyOneOfTokenA * nativeTokenPrice;
                priceDict[token].Add(new TokenPrice(price, (double)((new Rational(reserveCall.liquidity.Output.Liquidity) / Rational.Pow(10, 18)) * pairPrice)));

            }
        }
        return priceDict;
    }

    public static TokenPrice GetTokenPrice(Token token, BlockchainCurrency nativeToken, ReserveCall reserve, double nativeTokenPrice)
    {
        var tokenB = new Rational(token.Address.Equals(reserve.token0.Output?.TokenAddress, StringComparison.OrdinalIgnoreCase) ?
                                reserve.reserve.Output.Reserve1
                                : reserve.reserve.Output.Reserve0);

        var tokenA = new Rational(tokenB == reserve.reserve.Output.Reserve1 ?
                            reserve.reserve.Output.Reserve0
                            : reserve.reserve.Output.Reserve1);
        if (tokenB == 0 || tokenA == 0)
        {
            return new TokenPrice(0, 0);
        }
        var price = (double)((tokenB / Rational.Pow(10, nativeToken.Decimals)) / (tokenA / Rational.Pow(10, token.Decimals))) * nativeTokenPrice;
        var liquidity = (double)(tokenB / Rational.Pow(10, nativeToken.Decimals)) * nativeTokenPrice;
        return new TokenPrice(price, liquidity);
    }

    public static async Task<Dictionary<Token, List<ReserveV3Call>>> GetTokenPriceV3(Dictionary<Token, List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>>? pairDict, Nethereum.Web3.Web3 web3)
    {
        var reservesDict = new Dictionary<Token, List<ReserveV3Call>>();
        var callist = new List<IMulticallInputOutput>();

        foreach (var entry in pairDict.Keys)
        {
            reservesDict.Add(entry, new List<ReserveV3Call>());
            foreach (var pairResult in pairDict[entry])
            {
                var pair = pairResult.Output?.Pair;
                if (string.IsNullOrEmpty(pair) || pair == "0x0000000000000000000000000000000000000000") continue;
                var priceCall = new MulticallInputOutput<GetSlot0OfFunction, GetSlot0OfFunctionOutputDTOBase>(new GetSlot0OfFunction(), pair);
                var liquidityCall = new MulticallInputOutput<GetLiquidityOfFunction, GetLiquidityOfFunctionOutputDTOBase>(new GetLiquidityOfFunction(), pair);
                var token0Call = new MulticallInputOutput<GetToken0OfFunction, GetTokenOfFunctionOutputDTOBase>(new GetToken0OfFunction(), pair);
                var token1Call = new MulticallInputOutput<GetToken1OfFunction, GetTokenOfFunctionOutputDTOBase>(new GetToken1OfFunction(), pair);
                reservesDict[entry].Add(new ReserveV3Call(priceCall, liquidityCall, token0Call, token1Call));
                callist.Add(priceCall);
                callist.Add(liquidityCall);
                callist.Add(token0Call);
                callist.Add(token1Call);
            }
        }
        await web3.Eth.GetMultiQueryHandler().MultiCallAsync(2000, callist.ToArray()).ConfigureAwait(false);
        return reservesDict;
    }

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
        await web3.Eth.GetMultiQueryHandler().MultiCallAsync(2000, callist.ToArray()).ConfigureAwait(false);

        return reservesDict;
    }

    public static async Task<Dictionary<Token, List<BaseReserveCall>>> GetPairs(IList<Token> tokens, Nethereum.Web3.Web3 web3, int chainId, string chainNativeTokenAddress, IList<TradingPlattform> tradingPlattformList)
    {
        var pairDict = new Dictionary<Token, List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>>();
        var tokenPrices = new Dictionary<Token, List<BaseReserveCall>>();
        var callist = new List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>();

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
                var call = plattform.Version == PlattformVersion.V3 ?
                    new MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>(new GetPairV3OfFunction()
                    {
                        TokenA = token.Address,
                        TokenB = chainNativeTokenAddress,
                        Fee = plattform.Fee
                    }, plattform.Factory)
                    : new MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>(new GetPairOfFunction()
                    {
                        TokenA = token.Address,
                        TokenB = chainNativeTokenAddress
                    }, plattform.Factory);
                pairDict[token].Add(call);
                callist.Add(call);
            }
            try
            {
                await web3.Eth.GetMultiQueryHandler().MultiCallAsync(2000, callist.ToArray()).ConfigureAwait(false);
                if (plattform.Version == PlattformVersion.V3)
                {
                    var reserves = await GetTokenPriceV3(pairDict, web3);
                    foreach (var reserve in reserves)
                    {
                        if (!tokenPrices.ContainsKey(reserve.Key))
                        {
                            tokenPrices.Add(reserve.Key, new List<BaseReserveCall>());
                        }
                        tokenPrices[reserve.Key].AddRange(reserve.Value);
                    }
                }
                else
                {
                    var reserves = await GetReserves(pairDict, web3);
                    foreach (var reserve in reserves)
                    {
                        if (!tokenPrices.ContainsKey(reserve.Key))
                        {
                            tokenPrices.Add(reserve.Key, new List<BaseReserveCall>());
                        }
                        tokenPrices[reserve.Key].AddRange(reserve.Value);
                    }
                }
                pairDict.Clear();

            }
            catch (Exception ex)
            {
                //Failure on Plattform
                if (ex.Message.Equals("Smart contract error: Multicall3: call failed"))
                {
                    //TODO: Plattform invalid?
                    Console.WriteLine($"Plattform {plattform.Factory} is invalid");
                }
                pairDict.Clear();
                callist.Clear();
            }
        }

        return tokenPrices;
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
        _unitOfWork.Tokens.Update(tokens.ToArray());
        _unitOfWork.Commit();
    }
}
