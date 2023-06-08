using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Contracts.Dtos;
using AutotradingSignaler.Core.Web;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;
using Mapster;
using MediatR;

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
                _repository.Tokens.Add(token);
                _repository.Commit();
            }

            return token;

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
            var decimals = web3.Eth.ERC20.GetContractService(address).DecimalsQueryAsync();
            tasks.Add(decimals);
            var name = web3.Eth.ERC20.GetContractService(address).NameQueryAsync();
            tasks.Add(name);
            var symbol = web3.Eth.ERC20.GetContractService(address).SymbolQueryAsync();
            tasks.Add(symbol);
            await Task.WhenAll(tasks);
            if (string.IsNullOrEmpty(name.Result) || string.IsNullOrEmpty(symbol.Result))
            {
                return null;
            }
            return new Token
            {
                Address = address,
                ChainId = chainId,
                Name = name.Result,
                Symbol = symbol.Result,
                Decimals = decimals.Result,
            };

        }
    }
}
