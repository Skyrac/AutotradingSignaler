using AutotradingSignaler.Core.Web;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;
using MediatR;

namespace AutotradingSignaler.Core.Handlers.Commands.Web3
{
    public class AddWatchlistCommand : IRequest
    {
        public string Address { get; set; }
    }

    public class AddWatchlistCommandHandler : IRequestHandler<AddWatchlistCommand>
    {
        private readonly ILogger<AddWatchlistCommandHandler> _logger;
        private readonly IWeb3UnitOfWork _repository;
        private readonly Web3Service _web3Service;

        public AddWatchlistCommandHandler(ILogger<AddWatchlistCommandHandler> logger, IWeb3UnitOfWork repository, Web3Service web3Service)
        {
            _logger = logger;
            _repository = repository;
            _web3Service = web3Service;
        }

        public Task Handle(AddWatchlistCommand request, CancellationToken cancellationToken)
        {
            var watchlist = _repository.Watchlist.Where(w => w.Address == request.Address).Get();
            if (watchlist == null)
            {
                watchlist = new Contracts.Data.Watchlist
                {
                    Address = request.Address,
                    IsActive = true
                };
            }

            watchlist.IsActive = true;
            _repository.Watchlist.AddOrUpdate(watchlist);
            _repository.Commit();
            return Task.CompletedTask;
        }
    }
}
