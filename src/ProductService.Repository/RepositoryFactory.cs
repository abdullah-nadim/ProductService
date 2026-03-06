using ProductService.Models.Repositories;

namespace ProductService.Repository;

public class RepositoryFactory : IRepositoryFactory
{
    private readonly DatabaseContext _context;

    public RepositoryFactory(DatabaseContext context)
    {
        _context = context;
    }

    public IProductRepository GetProductRepository() => new ProductRepository(_context);

    public int Commit() => _context.SaveChanges();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // Resources are managed by the DI container (DatabaseContext is scoped).
        // Data must be committed explicitly via Commit() before disposal.
    }
}
