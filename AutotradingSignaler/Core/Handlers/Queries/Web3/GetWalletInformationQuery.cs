using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Contracts.Dtos;
using AutotradingSignaler.Core.Web;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;
using Mapster;
using MediatR;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Util;

namespace AutotradingSignaler.Core.Handlers.Queries.Web3
{
    public class GetWalletInformationQuery : IRequest<WalletDto>
    {
        public string Address { get; set; }
    }

    public class GetWalletInformationQueryHandler : IRequestHandler<GetWalletInformationQuery, WalletDto>
    {
        private readonly ILogger<GetWalletInformationQueryHandler> _logger;
        private readonly IWeb3UnitOfWork _repository;
        private readonly Web3Service _web3Service;

        public GetWalletInformationQueryHandler(IWeb3UnitOfWork web3UnitOfWork, ILogger<GetWalletInformationQueryHandler> logger, Web3Service web3Service)
        {
            _repository = web3UnitOfWork;
            _logger = logger;
            _web3Service = web3Service;
        }

        public async Task<WalletDto> Handle(GetWalletInformationQuery request, CancellationToken cancellationToken)
        {
            var tokens = _repository.Tokens.GetAll();
            var web3Instances = _web3Service.GetWeb3Instances();
            var tokenBalances = new List<TokenDto>();
            foreach (var web3Instance in web3Instances)
            {
                await ProcessWeb3Instance(request.Address, web3Instance, tokens, tokenBalances);
            }
            var trades = _repository.Trades.Where(t => t.Trader == request.Address).GetAll();

            return new WalletDto
            {
                Address = request.Address,
                Tokens = tokenBalances.OrderByDescending(t => t.Balance * (decimal)t.Price).ToList(),
                ProfitPerformance = trades.Sum(t => t.Profit),
                Trades = trades.Count()
            };
        }

        private async Task ProcessWeb3Instance(string address, KeyValuePair<int, Nethereum.Web3.Web3> web3Instance, IEnumerable<Token> tokens, IList<TokenDto> tokenBalances)
        {
            var tokensOfChain = tokens.Where(t => t.ChainId == web3Instance.Key);
            var callist = new List<IMulticallInputOutput>();
            foreach (var token in tokensOfChain)
            {
                var balanceOfMessage = new BalanceOfFunction() { Owner = address };
                var call = new MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO>(balanceOfMessage, token.Address);
                callist.Add(call);
            }
            await web3Instance.Value.Eth.GetMultiQueryHandler().MultiCallAsync(1000, callist.ToArray()).ConfigureAwait(false);

            for (var i = 0; i < callist.Count(); i++)
            {
                try
                {
                    if (callist[i] is MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO> result)
                    {
                        var balance = result.Output.Balance;
                        var token = tokensOfChain.ElementAt(i).Adapt<TokenDto>();
                        if (balance <= 0)
                        {
                            continue;
                        }
                        token.Balance = UnitConversion.Convert.FromWei(balance, token.Decimals);
                        tokenBalances.Add(token);
                    }
                }
                catch (Exception ex)
                {

                }

            }
            var balanceOfChainToken = await web3Instance.Value.Eth.GetBalance.SendRequestAsync(address);
            var chainInfo = _web3Service.GetBlockchainInfoOf(web3Instance.Key);
            var chainToken = tokensOfChain.FirstOrDefault(t => t.Symbol.Equals(chainInfo.NativeCurrency.Symbol, StringComparison.OrdinalIgnoreCase))?.Adapt<TokenDto>();
            if (chainToken == null)
            {
                chainToken = new TokenDto
                {
                    ChainId = chainInfo.ChainId,
                    Address = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
                    Name = chainInfo.NativeCurrency.Name,
                    Symbol = chainInfo.NativeCurrency.Symbol,
                    Decimals = chainInfo.NativeCurrency.Decimals,
                };

            }
            chainToken.Balance = UnitConversion.Convert.FromWei(balanceOfChainToken, chainToken.Decimals);
            tokenBalances.Add(chainToken);
        }
    }
}
