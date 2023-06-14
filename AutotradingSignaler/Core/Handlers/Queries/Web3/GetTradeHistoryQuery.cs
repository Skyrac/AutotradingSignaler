using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Contracts.Dtos;
using AutotradingSignaler.Persistence.Repositories.Interfaces;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;
using Mapster;
using MediatR;
using System.ComponentModel;

namespace AutotradingSignaler.Core.Handlers.Queries.Web3
{
    public class GetTradeHistoryQuery : IRequest<TradeHistoryDto>
    {
        public string? Trader { get; set; }
        public string? Token { get; set; }
        public int? Skip { get; set; }
        public int? Take { get; set; }
        public ListSortDirection? Sort { get; set; } = ListSortDirection.Descending;
    }

    public class GetTradeHistoryQueryHandler : IRequestHandler<GetTradeHistoryQuery, TradeHistoryDto>
    {
        private readonly ILogger<GetTradeHistoryQueryHandler> _logger;
        private readonly IWeb3UnitOfWork _repository;

        public GetTradeHistoryQueryHandler(ILogger<GetTradeHistoryQueryHandler> logger, IWeb3UnitOfWork repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public Task<TradeHistoryDto> Handle(GetTradeHistoryQuery request, CancellationToken cancellationToken)
        {
            IRepository<Trade> trades = _repository.Trades;
            if (!string.IsNullOrEmpty(request.Trader))
            {
                trades = trades.Where(t => t.Trader == request.Trader);
            }
            if (!string.IsNullOrEmpty(request.Token))
            {
                trades = trades.Where(t => t.TokenIn == request.Token || t.TokenOut == request.Token);
            }
            var totalCount = trades.Count();
            var tradeResults = trades.Sort(request.Sort.HasValue ? request.Sort.Value : ListSortDirection.Descending, t => t.Created).Skip(request.Skip).Take(request.Take).GetAll().ToList();
            var tradeList = tradeResults.Adapt<List<TradeDto>>();
            return Task.FromResult(new TradeHistoryDto
            {
                Trades = tradeList,
                Total = totalCount,
                CurrentOffset = request.Skip ?? 0
            });
        }
    }
}
