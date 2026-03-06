using Microsoft.EntityFrameworkCore;
using ProductService.Models;
using ProductService.Models.Repositories;

namespace ProductService.Repository;

public class ProductRepository : Repository<ProductModel>, IProductRepository
{
    public ProductRepository(DatabaseContext context) : base(context) { }

    public async Task<List<ProductModel>> ReadManyActiveAsync(CancellationToken cancellationToken = default)
        => await ReadManyAsync(p => p.IsActive && !p.IsDeleted, cancellationToken);

    public async Task<ProductModel?> ReadBySkuAsync(string sku, CancellationToken cancellationToken = default)
        => await ReadAsync(p => p.Sku == sku && !p.IsDeleted, cancellationToken);
}
