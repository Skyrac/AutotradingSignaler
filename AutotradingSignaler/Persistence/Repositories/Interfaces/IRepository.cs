using System.ComponentModel;
using System.Linq.Expressions;

namespace AutotradingSignaler.Persistence.Repositories.Interfaces;

public interface IRepository<T>
{
    IEnumerable<T> GetAll();
    T? Get(object id);
    List<T> GetAll(IEnumerable<long> id);
    void Add(T entity);
    void Add(bool setOutputIdentity = false, params T[] entities);
    void AddOrUpdate(params T[] entities);
    void AddOrUpdateOrDelete(params T[] entities);
    void Update(T entity);
    void Update(params T[] entities);
    void Delete(object id);
    void Delete(params T[] entities);
    int Count();
    void Save();
    void SaveBulk();
    IRepository<T> AsNoTracking();
    IRepository<T> Include<R>(Expression<Func<T, R>> expression);
    IRepository<T> ThenInclude<R, TProperty>(Expression<Func<R, TProperty>> expression);
    IRepository<T> ResetQuery();
    void AddOrUpdate(bool setOutputIdentity = false, params T[] entities);
    bool Any(Expression<Func<T, bool>> expression);
    IRepository<T> Where(Expression<Func<T, bool>> expression);
    T? Get();
    IRepository<T> Sort<TKey>(ListSortDirection? sortDirection, Expression<Func<T, TKey>>? expression);
    IRepository<T> Skip(int? amount);
    IRepository<T> Take(int? amount);
    IRepository<T> AsCacheable();
}

