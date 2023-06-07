using Microsoft.EntityFrameworkCore;

namespace Database.Utils.Repositories
{
    public interface IDatabaseContext
    {
        public DbSet<TEntity> Get<TEntity>() where TEntity : class;
    }
}
