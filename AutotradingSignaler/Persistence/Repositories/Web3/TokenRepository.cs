using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Persistence.Repositories.Web3.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutotradingSignaler.Persistence.Repositories.Web3
{
    public class TokenRepository : Repository<Token>, ITokenRepository
    {
        public TokenRepository(DbContext context) : base(context)
        {
        }
    }
}
