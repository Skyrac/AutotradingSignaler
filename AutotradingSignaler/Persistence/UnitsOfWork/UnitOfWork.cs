using AutotradingSignaler.Persistence.UnitsOfWork.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutotradingSignaler.Persistence.UnitsOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        protected virtual DbContext _context { get; }

        public UnitOfWork(DbContext context)
        {
            _context = context;
        }

        public void Commit()
        {
            _context.SaveChanges();
        }
    }
}
