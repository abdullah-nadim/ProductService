using Microsoft.EntityFrameworkCore;
using Core.Models;
using Core.Repositories;
using System.Linq.Expressions;

namespace ProductService.Repository;

public abstract class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    public Repository(DbContext dbContext)
    {
        _Context = dbContext;
        _Collection = _Context.Set<TEntity>();
    }

    #region Create
    public virtual void Create(TEntity entity) => _Collection.Add(entity);
    public virtual async Task CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await _Collection.AddAsync(entity, cancellationToken);
    public void Create(List<TEntity> entities) => _Collection.AddRange(entities);
    public virtual async Task CreateAsync(List<TEntity> entities, CancellationToken cancellationToken = default)
        => await _Collection.AddRangeAsync(entities, cancellationToken);
    #endregion

    #region Read
    public virtual TEntity Read(object key) => _Collection.Find(key)!;
    public virtual async Task<TEntity> ReadAsync(object key, CancellationToken cancellationToken = default)
        => (await _Collection.FindAsync([key], cancellationToken))!;

    public virtual List<TEntity> ReadMany() => _Collection.ToList();
    public virtual async Task<List<TEntity>> ReadManyAsync(CancellationToken cancellationToken = default)
        => await _Collection.ToListAsync(cancellationToken);

    public PagedEntities<TEntity> ReadMany(int pageNumber = 1, int pageSize = 10) => new()
    {
        Items = _Collection.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalItems = _Collection.Count()
    };

    public virtual async Task<PagedEntities<TEntity>> ReadManyAsync(int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default) => new()
    {
        Items = await _Collection.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken),
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalItems = await _Collection.CountAsync(cancellationToken)
    };
    #endregion

    #region Update
    public virtual void Update(TEntity entity) => _Collection.Update(entity);
    public virtual void Update(List<TEntity> entities) => _Collection.UpdateRange(entities);
    #endregion

    #region Delete
    public virtual void Delete(object key) => Delete(_Collection.Find(key)!);
    public virtual async Task DeleteAsync(object key, CancellationToken cancellationToken = default)
        => Delete((await _Collection.FindAsync([key], cancellationToken))!);
    public virtual void Delete(TEntity entity) => _Collection.Remove(entity);
    public virtual void Delete(List<TEntity> entities) => _Collection.RemoveRange(entities);
    #endregion

    #region Count
    public virtual int Count() => _Collection.Count();
    public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _Collection.CountAsync(cancellationToken);
    #endregion

    #region Protected helpers
    protected TEntity? Read(Expression<Func<TEntity, bool>> predicate)
        => _Collection.FirstOrDefault(predicate);

    protected async Task<TEntity?> ReadAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => await _Collection.FirstOrDefaultAsync(predicate, cancellationToken);

    protected List<TEntity> ReadMany(Expression<Func<TEntity, bool>> predicate)
        => _Collection.Where(predicate).ToList();

    protected async Task<List<TEntity>> ReadManyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => await _Collection.Where(predicate).ToListAsync(cancellationToken);

    protected async Task<PagedEntities<TEntity>> ReadManyAsync(
        Expression<Func<TEntity, bool>> predicate,
        int pageNumber = 1, int pageSize = 10,
        CancellationToken cancellationToken = default) => new()
    {
        Items = await _Collection.Where(predicate).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken),
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalItems = await _Collection.CountAsync(predicate, cancellationToken)
    };

    protected IQueryable<TEntity> GetQueryableWithIncludes(List<string> includeProperties)
    {
        IQueryable<TEntity> query = _Collection.AsQueryable();
        includeProperties?.ForEach(p => query = query.Include(p));
        return query;
    }
    #endregion

    protected DbContext _Context;
    protected DbSet<TEntity> _Collection;
}
