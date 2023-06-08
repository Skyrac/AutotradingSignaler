using AutotradingSignaler.Contracts.Dtos;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;
using Mapster;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace AutotradingSignaler.Core.Handlers.Queries.Web3
{
    public class GetTradeHistoryQuery : IRequest<List<TradeDto>>
    {
        [Required]
        public string Trader { get; set; }
        public int? Skip { get; set; }
        public int? Take { get; set; }
    }

    public class GetTradeHistoryQueryHandler : IRequestHandler<GetTradeHistoryQuery, List<TradeDto>>
    {
        private readonly ILogger<GetTradeHistoryQueryHandler> _logger;
        private readonly IWeb3UnitOfWork _repository;

        public GetTradeHistoryQueryHandler(ILogger<GetTradeHistoryQueryHandler> logger, IWeb3UnitOfWork repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public Task<List<TradeDto>> Handle(GetTradeHistoryQuery request, CancellationToken cancellationToken)
        {
            var trades = _repository.Trades.Where(t => t.Trader == request.Trader).Skip(request.Skip).Take(request.Take).GetAll().ToList();
            var tradeList = trades.Adapt<List<TradeDto>>();
            return Task.FromResult(tradeList);
        }
    }
}
