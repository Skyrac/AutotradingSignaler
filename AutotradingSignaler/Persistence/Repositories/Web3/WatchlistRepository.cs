using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Persistence.Repositories.Web3.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutotradingSignaler.Persistence.Repositories.Web3
{
    public class WatchlistRepository : Repository<Watchlist>, IWatchlistRepository
    {
        public WatchlistRepository(DbContext context) : base(context)
        {
        }
    }
}
