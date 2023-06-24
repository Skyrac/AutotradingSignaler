using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Contracts.Dtos;
using AutotradingSignaler.Persistence.Repositories.Web3.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace AutotradingSignaler.Persistence.Repositories.Web3;

public class TradeRepository : Repository<Trade>, ITradeRepository
{
    public TradeRepository(DbContext context) : base(context)
    {
    }

    public List<TraderDto> GetBestPerformingTraders(string? token, int? skip, int? take, ListSortDirection? sort)
    {
        var query = _query.Select(t => new { t.Trader, t.Profit, t.TokenIn, t.TokenOut }).Where(t => !Double.IsNaN(t.Profit));
        if (!string.IsNullOrEmpty(token) && token != "undefined")
        {
            query = query.Where(t => t.TokenIn == token || t.TokenOut == token);
        }
        var selectedQuery = query.GroupBy(t => t.Trader).Select(g =>
            new TraderDto
            {
                Address = g.First().Trader,
                Profit = g.Sum(t => t.Profit),
                Trades = g.Count()
            }).Where(p => p.Profit > 0);
        if (sort == null)
        {
            sort = ListSortDirection.Descending;
        }

        var sortedQuery = (sort == ListSortDirection.Descending ?
            selectedQuery.OrderByDescending(t => t.Profit)
            : selectedQuery.OrderBy(t => t.Profit)).ThenByDescending(t => t.Trades).AsQueryable();

        if (skip.HasValue)
        {
            sortedQuery = sortedQuery.Skip(skip.Value);
        }
        if (take.HasValue)
        {
            sortedQuery = sortedQuery.Take(take.Value);
        }

        return sortedQuery.ToList();
    }
}
