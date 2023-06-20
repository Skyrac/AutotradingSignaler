using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Persistence.Repositories.Web3.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutotradingSignaler.Persistence.Repositories.Web3;

public class TradingPlattformRepository : Repository<TradingPlattform>, ITradingPlattformRepository
{
    public TradingPlattformRepository(DbContext context) : base(context)
    {
    }
}
