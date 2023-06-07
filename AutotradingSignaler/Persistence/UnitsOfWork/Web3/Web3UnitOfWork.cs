using AutotradingSignaler.Persistence.Repositories.Web3;
using AutotradingSignaler.Persistence.Repositories.Web3.Interfaces;
using AutotradingSignaler.Persistence.UnitsOfWork.Web3.Interfaces;

namespace AutotradingSignaler.Persistence.UnitsOfWork.Web3;

public class Web3UnitOfWork : UnitOfWork, IWeb3UnitOfWork
{
    public ITokenRepository Tokens => new TokenRepository(_context);
    public Web3UnitOfWork(BaseMigrationDbContext context) : base(context)
    {
    }
}
