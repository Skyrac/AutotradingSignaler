using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Contracts.Dtos;
using AutotradingSignaler.Persistence.Repositories.Interfaces;
using System.ComponentModel;

namespace AutotradingSignaler.Persistence.Repositories.Web3.Interfaces;

public interface ITradeRepository : IRepository<Trade>
{
    List<TraderDto> GetBestPerformingTraders(string? token, int? skip, int? take, ListSortDirection? sort);
}
