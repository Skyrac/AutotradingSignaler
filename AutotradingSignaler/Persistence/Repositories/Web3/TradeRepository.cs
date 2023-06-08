using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Persistence.Repositories.Web3.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutotradingSignaler.Persistence.Repositories.Web3;

public class TradeRepository : Repository<Trade>, ITradeRepository
{
    public TradeRepository(DbContext context) : base(context)
    {
    }
}
