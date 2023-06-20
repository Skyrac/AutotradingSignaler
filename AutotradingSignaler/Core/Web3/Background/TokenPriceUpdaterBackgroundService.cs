using _1InchApi;
using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Contracts.Web3.Contracts;
using AutotradingSignaler.Core.Web;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Web3;

namespace AutotradingSignaler.Core.Web3.Background;

public class TokenPriceUpdaterBackgroundService : BackgroundService
{
    private readonly ILogger<TokenPriceUpdaterBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Web3Service _web3Service;

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
                    var price = await OneInchApiWrapper.GetQuote((Chain)web3Instance.Key, chainInfo.StableCoin.Address!, chainInfo.NativeCurrency.Address, 1, chainInfo.StableCoin.Decimals);

                    var tokens = GetTokens(web3Instance.Key);
                    //get pairs
                    var pairDict = await GetPairs(tokens, web3Instance.Value, chainInfo.ChainId, chainInfo.NativeCurrency.Address);
                    var reservesDict = await GetReserves(pairDict, web3Instance.Value);
                    //get reserves from pair
                    foreach (var token in tokens)
                    {
                        if (token.Address.Equals(chainInfo.StableCoin.Address, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }



                        _logger.LogInformation("Price Update for Token: {0} {1} = 1 USD", token.Address, price?.toTokenAmount);
                        await Task.Delay(1000);
                    }
                    //2. Get BNB Price of all Tokens from Router
                    await Task.Delay(60000 * 5);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"");
            }
        }
    }

    private record ReserveCall(MulticallInputOutput<GetReserveOfFunction, GetReserveOfFunctionOutputDTOBase> reserve, MulticallInputOutput<GetToken0OfFunction, GetTokenOfFunctionOutputDTOBase> token0, MulticallInputOutput<GetToken1OfFunction, GetTokenOfFunctionOutputDTOBase> token1);

    private async Task<Dictionary<Token, List<ReserveCall>>> GetReserves(Dictionary<Token, List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>>? pairDict, Nethereum.Web3.Web3 web3)
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
        await web3.Eth.GetMultiQueryHandler().MultiCallAsync(callist.ToArray()).ConfigureAwait(false);

        return reservesDict;
    }

    private async Task<Dictionary<Token, List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>>?> GetPairs(IList<Token> tokens, Nethereum.Web3.Web3 web3, int chainId, string chainNativeTokenAddress)
    {
        var pairDict = new Dictionary<Token, List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>>();
        var callist = new List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>();
        IList<TradingPlattform> tradingPlattformList = null;
        using (var scope = _scopeFactory.CreateScope())
        {
            var _unitOfWork = scope.ServiceProvider.GetRequiredService<IWeb3UnitOfWork>();
            tradingPlattformList = _unitOfWork.TradingPlattforms.Where(tp => tp.ChainId == chainId && tp.IsValid).GetAll().ToList();
        }
        tradingPlattformList.Add(new TradingPlattform()
        {
            ChainId = chainId,
            Factory = "0xcA143Ce32Fe78f1f7019d7d551a6402fC5350c73"
        });
        if (tradingPlattformList == null)
        {
            return null;
        }
        foreach (var token in tokens)
        {
            if (token.Address == "0x0000000000000000000000000000000000000000" || token.Address == "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee" || token.Address == chainNativeTokenAddress) continue;
            if (!pairDict.ContainsKey(token))
            {
                pairDict.Add(token, new List<MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>>());
            }
            foreach (var plattform in tradingPlattformList)
            {
                var message = new GetPairOfFunction()
                {
                    TokenA = token.Address,
                    TokenB = chainNativeTokenAddress
                };
                var call = new MulticallInputOutput<GetPairOfFunction, GetPairOfFunctionOutputDTOBase>(message, plattform.Factory);
                pairDict[token].Add(call);
                callist.Add(call);
            }
        }
        await web3.Eth.GetMultiQueryHandler().MultiCallAsync(callist.ToArray()).ConfigureAwait(false);
        return pairDict;
    }



    private IList<Token> GetTokens(int chain)
    {
        using var scope = _scopeFactory.CreateScope();
        var _unitOfWork = scope.ServiceProvider.GetRequiredService<IWeb3UnitOfWork>();
        return _unitOfWork.Tokens.Where(t => t.ChainId == chain).GetAll().ToList();
    }
}
