using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Core.Services.Interfaces;
using Database.Utils.Repositories;
using Microsoft.EntityFrameworkCore;
namespace AutotradingSignaler.Persistence
{
    public class BaseMigrationDbContext : DatabaseContext<BaseMigrationDbContext>
    {
        public DbSet<Token> Tokens { get; set; }
        public DbSet<Trade> Trades { get; set; }
        public DbSet<Watchlist> Watchlist { get; set; }
        public BaseMigrationDbContext(DbContextOptions<BaseMigrationDbContext> options, IUserService user) : base(options, user)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase("Test").UseSnakeCaseNamingConvention();
            base.OnConfiguring(optionsBuilder);
        }
    }
}
