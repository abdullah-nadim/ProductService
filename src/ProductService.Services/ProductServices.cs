using Infrastructure.Redis.Configuration;
using Infrastructure.Redis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductService.Models;
using ProductService.Models.Repositories;
using ProductService.Repository;

namespace ProductService.Services;

public class ProductServices : BaseServices, IProductServices
{
    private readonly IProductEventPublisher _eventPublisher;
    private readonly ILogger<ProductServices> _logger;

    public ProductServices(
        DatabaseContext context,
        IProductEventPublisher eventPublisher,
        ILogger<ProductServices> logger) : base(context)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public ProductServices(
        DatabaseContext context,
        IProductEventPublisher eventPublisher,
        ILogger<ProductServices> logger,
        ICacheService cacheService,
        IOptions<CacheExpirationOptions> cacheExpiration) : base(context, cacheService, cacheExpiration)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<long> CreateProduct(ProductModel model)
    {
        using IRepositoryFactory factory = new Repository.RepositoryFactory(_Context);
        await factory.GetProductRepository().CreateAsync(model);
        factory.Commit();

        await _eventPublisher.PublishProductCreatedAsync(new ProductMessage
        {
            ProductId = model.Id,
            ProductName = model.Name,
            Sku = model.Sku,
            EventType = "Created",
            OccurredAt = DateTime.UtcNow
        });

        _logger.LogInformation("Created product {Id} - {Name}", model.Id, model.Name);

        if (_cacheService != null)
        {
            await _cacheService.RemoveByPatternAsync("product:*");
        }

        return model.Id;
    }

    public async Task UpdateProduct(ProductModel updatedModel)
    {
        using IRepositoryFactory factory = new Repository.RepositoryFactory(_Context);
        var repo = factory.GetProductRepository();
        var existing = await repo.ReadAsync(updatedModel.Id);
        repo.Update(existing.Update(updatedModel));
        factory.Commit();

        await _eventPublisher.PublishProductUpdatedAsync(new ProductMessage
        {
            ProductId = updatedModel.Id,
            ProductName = updatedModel.Name,
            Sku = updatedModel.Sku,
            EventType = "Updated",
            OccurredAt = DateTime.UtcNow
        });

        if (_cacheService != null)
        {
            await _cacheService.RemoveAsync($"product:{updatedModel.Id}");
            await _cacheService.RemoveByPatternAsync("product:list:*");
        }
    }

    public async Task<ProductModel?> GetProductById(long id)
    {
        if (_cacheService != null)
        {
            var cacheKey = $"product:{id}";
            var expiry = _cacheExpiration?.MediumLived ?? TimeSpan.FromMinutes(30);
            return await _cacheService.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    using IRepositoryFactory factory = new Repository.RepositoryFactory(_Context);
                    return await factory.GetProductRepository().ReadAsync(id);
                },
                expiry);
        }

        using IRepositoryFactory directFactory = new Repository.RepositoryFactory(_Context);
        return await directFactory.GetProductRepository().ReadAsync(id);
    }

    public async Task<List<ProductModel>> GetAllProducts()
    {
        if (_cacheService != null)
        {
            var cacheKey = "product:list:all";
            var expiry = _cacheExpiration?.MediumLived ?? TimeSpan.FromMinutes(30);
            return await _cacheService.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    using IRepositoryFactory factory = new Repository.RepositoryFactory(_Context);
                    return await factory.GetProductRepository().ReadManyAsync();
                },
                expiry) ?? [];
        }

        using IRepositoryFactory directFactory = new Repository.RepositoryFactory(_Context);
        return await directFactory.GetProductRepository().ReadManyAsync();
    }

    public async Task DeleteProduct(long id)
    {
        using IRepositoryFactory factory = new Repository.RepositoryFactory(_Context);
        var repo = factory.GetProductRepository();
        var existing = await repo.ReadAsync(id);
        existing.IsActive = false;
        existing.IsDeleted = true;
        repo.Update(existing);
        factory.Commit();

        await _eventPublisher.PublishProductDeletedAsync(new ProductMessage
        {
            ProductId = id,
            EventType = "Deleted",
            OccurredAt = DateTime.UtcNow
        });

        _logger.LogInformation("Deleted product {Id}", id);

        if (_cacheService != null)
        {
            await _cacheService.RemoveAsync($"product:{id}");
            await _cacheService.RemoveByPatternAsync("product:list:*");
        }
    }

    public async Task AdjustStockAsync(long productId, int quantityChange)
    {
        using IRepositoryFactory factory = new Repository.RepositoryFactory(_Context);
        var repo = factory.GetProductRepository();
        var product = await repo.ReadAsync(productId);
        product.StockQuantity = Math.Max(0, product.StockQuantity + quantityChange);
        repo.Update(product);
        factory.Commit();

        _logger.LogInformation("Adjusted stock for product {Id} by {Change}. New qty: {Qty}",
            productId, quantityChange, product.StockQuantity);

        if (_cacheService != null)
        {
            await _cacheService.RemoveAsync($"product:{productId}");
            await _cacheService.RemoveByPatternAsync("product:list:*");
        }
    }

    public async Task<List<ProductModel>> GetAllActiveProducts()
    {
        if (_cacheService != null)
        {
            var cacheKey = "product:list:active";
            var expiry = _cacheExpiration?.MediumLived ?? TimeSpan.FromMinutes(30);
            return await _cacheService.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    using IRepositoryFactory factory = new Repository.RepositoryFactory(_Context);
                    return await factory.GetProductRepository().ReadManyActiveAsync();
                },
                expiry) ?? [];
        }

        using IRepositoryFactory directFactory = new Repository.RepositoryFactory(_Context);
        return await directFactory.GetProductRepository().ReadManyActiveAsync();
    }
}
