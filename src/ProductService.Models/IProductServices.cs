using ProductService.Models;

namespace ProductService.Models;

public interface IProductServices
{
    Task<long> CreateProduct(ProductModel model);
    Task UpdateProduct(ProductModel updatedModel);
    Task<ProductModel?> GetProductById(long id);
    Task<List<ProductModel>> GetAllProducts();
    Task<List<ProductModel>> GetAllActiveProducts();
    Task DeleteProduct(long id);
    Task AdjustStockAsync(long productId, int quantityChange);
}
