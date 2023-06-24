using _1InchApi;
using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Core.Web;
using AutotradingSignaler.Core.Web3.Background;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;
using MediatR;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;

namespace AutotradingSignaler.Core.Handlers.Commands.Web3
{
    public class AddTokenCommand : IRequest<Token>
    {
        public int ChainId { get; set; }
        public string Address { get; set; }
    }

    public class AddTokenCommandHandler : IRequestHandler<AddTokenCommand, Token>
    {
        private readonly ILogger<AddTokenCommandHandler> _logger;
        private readonly IWeb3UnitOfWork _repository;
        private readonly Web3Service _web3Service;

        public AddTokenCommandHandler(ILogger<AddTokenCommandHandler> logger, IWeb3UnitOfWork repository, Web3Service web3Service)
        {
            _logger = logger;
            _repository = repository;
            _web3Service = web3Service;
        }

        public async Task<Token> Handle(AddTokenCommand request, CancellationToken cancellationToken)
        {
            //Check if token is already in list
            var token = _repository.Tokens.Where(token => token.ChainId == request.ChainId && token.Address == request.Address).Get();
            //Not: Get Token information
            if (token == null)
            {
                token = await RetrieveTokenData(request.Address, request.ChainId);
                if (token == null)
                {
                    return null;
                }
                await AddTokenPriceData(token, request.ChainId);
                _repository.Tokens.Add(token);
                _repository.Commit();
            }

            return token;

        }

        private async Task AddTokenPriceData(Token token, int chainId)
        {
            var web3 = _web3Service.GetWeb3InstanceOf(chainId);
            var chainInfo = _web3Service.GetBlockchainInfoOf(chainId);
            if (web3 == null)
            {
                //TODO: Throw error
                return;
            }

            if (chainInfo.StableCoin == null)
            {
                return;
            }

            var tokenPrices = await TokenPriceUpdaterBackgroundService.ProcessTokensAndReceivePriceData(web3,
                    chainInfo,
                    new List<Token> { token },
                    _repository.TradingPlattforms.Where(t => t.ChainId == chainId && t.IsValid)
                .GetAll()
                .ToList());

            token.Price = tokenPrices.FirstOrDefault()?.Price ?? 0;
        }

        private async Task<Token?> RetrieveTokenData(string address, int chainId)
        {
            var web3 = _web3Service.GetWeb3InstanceOf(chainId);
            if (web3 == null)
            {
                //TODO: Throw error
                return null;
            }
            var tasks = new List<Task>();
            var callist = new List<IMulticallInputOutput>();
            var decimalMessage = new DecimalsFunction();
            var decimalsResult = new MulticallInputOutput<DecimalsFunction, DecimalsOutputDTO>(decimalMessage, address);
            callist.Add(decimalsResult);
            var nameMessage = new NameFunction();
            var nameResult = new MulticallInputOutput<NameFunction, NameOutputDTO>(nameMessage, address);
            callist.Add(nameResult);
            var symbolMessage = new SymbolFunction();
            var symbolResult = new MulticallInputOutput<SymbolFunction, SymbolOutputDTO>(symbolMessage, address);
            callist.Add(symbolResult);
            //var decimals = web3.Eth.ERC20.GetContractService(address).DecimalsQueryAsync();
            //tasks.Add(decimals);
            //var name = web3.Eth.ERC20.GetContractService(address).NameQueryAsync();
            //tasks.Add(name);
            //var symbol = web3.Eth.ERC20.GetContractService(address).SymbolQueryAsync();
            //tasks.Add(symbol);
            //await Task.WhenAll(tasks);
            await web3.Eth.GetMultiQueryHandler().MultiCallAsync(callist.ToArray()).ConfigureAwait(false);

            if (string.IsNullOrEmpty(nameResult.Output.Name) || string.IsNullOrEmpty(symbolResult.Output.Symbol))
            {
                return null;
            }
            return new Token
            {
                Address = address,
                ChainId = chainId,
                Name = nameResult.Output.Name,
                Symbol = symbolResult.Output.Symbol,
                Decimals = decimalsResult.Output.Decimals,
            };

        }
    }
}
