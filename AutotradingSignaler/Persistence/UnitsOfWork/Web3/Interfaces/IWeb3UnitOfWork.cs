using AutotradingSignaler.Persistence.Repositories.Web3.Interfaces;
using AutotradingSignaler.Persistence.UnitsOfWork.Interfaces;

namespace AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;

public interface IWeb3UnitOfWork : IUnitOfWork
{
    ITokenRepository Tokens { get; }
    IWatchlistRepository Watchlist { get; }
    ITradeRepository Trades { get; }
}
