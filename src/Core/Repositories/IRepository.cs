using Core.Models;

namespace Core.Repositories;

public interface IRepository<TEntity> where TEntity : class
{
    void Create(TEntity entity);
    Task CreateAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Create(List<TEntity> entities);
    Task CreateAsync(List<TEntity> entities, CancellationToken cancellationToken = default);

    TEntity Read(object key);
    Task<TEntity> ReadAsync(object key, CancellationToken cancellationToken = default);

    List<TEntity> ReadMany();
    Task<List<TEntity>> ReadManyAsync(CancellationToken cancellationToken = default);
    PagedEntities<TEntity> ReadMany(int pageNumber, int pageSize);
    Task<PagedEntities<TEntity>> ReadManyAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    void Update(TEntity entity);
    void Update(List<TEntity> entities);

    void Delete(object key);
    Task DeleteAsync(object key, CancellationToken cancellationToken = default);
    void Delete(TEntity entity);
    void Delete(List<TEntity> entities);

    int Count();
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
