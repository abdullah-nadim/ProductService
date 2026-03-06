using Core.Repositories;

namespace ProductService.Models.Repositories;

public interface IProductRepository : IRepository<ProductModel>
{
    Task<List<ProductModel>> ReadManyActiveAsync(CancellationToken cancellationToken = default);
    Task<ProductModel?> ReadBySkuAsync(string sku, CancellationToken cancellationToken = default);
}
