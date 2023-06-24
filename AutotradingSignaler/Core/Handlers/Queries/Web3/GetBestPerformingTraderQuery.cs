using AutotradingSignaler.Contracts.Dtos;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;
using MediatR;
using System.ComponentModel;

namespace AutotradingSignaler.Core.Handlers.Queries.Web3;

public class GetBestPerformingTraderQuery : IRequest<List<TraderDto>>
{
    public string? Token { get; set; }
    public int? Skip { get; set; }
    public int? Take { get; set; }
    public ListSortDirection? Sort { get; set; } = ListSortDirection.Descending;
}

public class GetBestPerformingTraderQueryHandler : IRequestHandler<GetBestPerformingTraderQuery, List<TraderDto>>
{
    private readonly ILogger<GetBestPerformingTraderQueryHandler> _logger;
    private readonly IWeb3UnitOfWork _repository;

    public GetBestPerformingTraderQueryHandler(ILogger<GetBestPerformingTraderQueryHandler> logger, IWeb3UnitOfWork repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task<List<TraderDto>> Handle(GetBestPerformingTraderQuery request, CancellationToken cancellationToken)
    {
        var traders = _repository.Trades.GetBestPerformingTraders(request.Token, request.Skip, request.Take, request.Sort);
        return traders;
    }
}