using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.ComponentModel;
using System.Linq.Expressions;
using EFCoreSecondLevelCacheInterceptor;
using AutotradingSignaler.Contracts.Data;
using AutotradingSignaler.Persistence.Repositories.Interfaces;

namespace AutotradingSignaler.Persistence.Repositories;
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly DbContext _context;
    protected readonly DbSet<T> _dbSet;
    protected IQueryable<T> _query;

    public Repository(DbContext context)
    {
        _context = context;
        _dbSet = _context.Set<T>();
        _query = _dbSet.AsQueryable();
    }

    public void Add(T entity)
    {
        _dbSet.Add(entity);
    }

    public void Add(bool setOutputIdentity = false, params T[] entities)
    {
        _context.BulkInsert(entities, r => r.SetOutputIdentity = setOutputIdentity);
    }

    public bool Any(Expression<Func<T, bool>> expression) => _query.Any(expression);

    public void AddOrUpdate(params T[] entities)
    {
        _context.BulkInsertOrUpdate(entities);
    }

    public void AddOrUpdate(bool setOutputIdentity = false, params T[] entities)
    {
        _context.BulkInsertOrUpdate(entities, r => r.SetOutputIdentity = setOutputIdentity);
    }

    public void AddOrUpdateOrDelete(params T[] entities)
    {
        _context.BulkInsertOrUpdateOrDelete(entities);
    }

    public int Count()
    {
        return _query.Count();
    }

    public void Delete(object id)
    {
        var entity = Get(id);
        if (entity != null)
        {
            if (_context.Entry(entity).State == EntityState.Detached)
            {
                _dbSet.Attach(entity);
            }
            _dbSet.Remove(entity);
        }
    }

    public void Delete(params T[] entities)
    {
        _context.BulkDelete(entities);
    }

    public T? Get(object id)
    {
        return _query.FirstOrDefault(t => t.Id.Equals(id));
    }

    public List<T> GetAll(IEnumerable<long> ids)
    {
        return _query.Where(item => ids.Contains(item.Id)).ToList();
    }

    public T? Get()
    {
        return _query.FirstOrDefault();
    }

    public IEnumerable<T> GetAll()
    {
        return _query.AsEnumerable();
    }

    public void Update(T entity)
    {
        _dbSet.Attach(entity);
        _context.Entry(entity).State = EntityState.Modified;
    }



    public void Update(params T[] entities)
    {
        _context.BulkUpdate(entities);
    }

    public void Save()
    {
        _context.SaveChanges();
    }

    public void SaveBulk()
    {
        _context.BulkSaveChanges();
    }

    public IRepository<T> AsNoTracking()
    {
        _query = _query.AsNoTracking();
        return this;
    }

    public IRepository<T> Include<R>(Expression<Func<T, R>> expression)
    {
        _query = _query.Include(expression);
        return this;
    }

    public IRepository<T> ThenInclude<R, TProperty>(Expression<Func<R, TProperty>> expression)
    {
        _query = ((IIncludableQueryable<T, R>)_query).ThenInclude(expression);
        return this;
    }

    public IRepository<T> Where(Expression<Func<T, bool>> expression)
    {
        _query = _query.Where(expression);
        return this;
    }

    public IRepository<T> Take(int? amount)
    {
        if (amount.HasValue && amount.Value > 0)
        {
            _query = _query.Take(amount.Value);
        }
        return this;
    }

    public IRepository<T> Skip(int? amount)
    {
        if (amount.HasValue && amount.Value > 0)
        {
            _query = _query.Skip(amount.Value);
        }
        return this;
    }

    public IRepository<T> Sort<TKey>(ListSortDirection? sortDirection, Expression<Func<T, TKey>>? expression)
    {
        if (sortDirection.HasValue && expression != null)
        {
            _query = sortDirection.Value == ListSortDirection.Ascending ? _query.OrderBy(expression) : _query.OrderByDescending(expression);
        }
        return this;
    }

    public IRepository<T> ResetQuery()
    {
        _query = _dbSet.AsQueryable();
        return this;
    }

    public IRepository<T> AsCacheable()
    {
        _query = _query.Cacheable();
        return this;
    }
}

