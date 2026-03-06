using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ProductService.Models;
using ProductService.Repository;
using ProductService.Services;

namespace ProductService.Tests;

public class ProductServicesTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly Mock<IProductEventPublisher> _eventPublisher;
    private readonly Mock<ILogger<Services.ProductServices>> _logger;
    private readonly Services.ProductServices _sut;

    public ProductServicesTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new DatabaseContext(options);
        _eventPublisher = new Mock<IProductEventPublisher>();
        _logger = new Mock<ILogger<Services.ProductServices>>();
        _sut = new Services.ProductServices(_context, _eventPublisher.Object, _logger.Object);
    }

    // ── CreateProduct ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProduct_ShouldPersistProduct_AndReturnId()
    {
        var model = BuildProduct("Laptop", "LAP-001");

        var id = await _sut.CreateProduct(model);

        Assert.True(id > 0);
        Assert.Equal(1, await _context.Products.CountAsync());
    }

    [Fact]
    public async Task CreateProduct_ShouldPublishCreatedEvent()
    {
        var model = BuildProduct("Phone", "PHN-001");

        await _sut.CreateProduct(model);

        _eventPublisher.Verify(p => p.PublishProductCreatedAsync(
            It.Is<ProductMessage>(m => m.EventType == "Created")), Times.Once);
    }

    // ── UpdateProduct ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProduct_ShouldUpdateFields_AndPublishEvent()
    {
        var model = BuildProduct("OldName", "SKU-001");
        await _sut.CreateProduct(model);

        var updated = BuildProduct("NewName", "SKU-001");
        updated.Id = model.Id;
        updated.Description = "Updated description";
        await _sut.UpdateProduct(updated);

        var saved = await _context.Products.FindAsync(model.Id);
        Assert.Equal("NewName", saved!.Name);
        Assert.Equal("Updated description", saved.Description);

        _eventPublisher.Verify(p => p.PublishProductUpdatedAsync(
            It.Is<ProductMessage>(m => m.EventType == "Updated" && m.ProductId == model.Id)), Times.Once);
    }

    // ── GetProductById ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProductById_ShouldReturnProduct_WhenExists()
    {
        var model = BuildProduct("Tablet", "TAB-001");
        await _sut.CreateProduct(model);

        var result = await _sut.GetProductById(model.Id);

        Assert.NotNull(result);
        Assert.Equal("Tablet", result.Name);
    }

    [Fact]
    public async Task GetProductById_ShouldReturnNull_WhenNotFound()
    {
        var result = await _sut.GetProductById(9999);

        Assert.Null(result);
    }

    // ── GetAllProducts ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllProducts_ShouldReturnAllProducts()
    {
        await _sut.CreateProduct(BuildProduct("A", "SKU-A"));
        await _sut.CreateProduct(BuildProduct("B", "SKU-B"));
        await _sut.CreateProduct(BuildProduct("C", "SKU-C"));

        var result = await _sut.GetAllProducts();

        Assert.Equal(3, result.Count);
    }

    // ── GetAllActiveProducts ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAllActiveProducts_ShouldReturnOnlyActiveNonDeleted()
    {
        await _sut.CreateProduct(BuildProduct("Active1", "SKU-A1", isActive: true));
        await _sut.CreateProduct(BuildProduct("Active2", "SKU-A2", isActive: true));
        await _sut.CreateProduct(BuildProduct("Inactive", "SKU-I1", isActive: false));

        var result = await _sut.GetAllActiveProducts();

        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.True(p.IsActive));
    }

    // ── DeleteProduct ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteProduct_ShouldSoftDelete_AndPublishEvent()
    {
        var model = BuildProduct("ToDelete", "DEL-001");
        await _sut.CreateProduct(model);

        await _sut.DeleteProduct(model.Id);

        var saved = await _context.Products.FindAsync(model.Id);
        Assert.NotNull(saved);
        Assert.False(saved!.IsActive);
        Assert.True(saved.IsDeleted);

        _eventPublisher.Verify(p => p.PublishProductDeletedAsync(
            It.Is<ProductMessage>(m => m.EventType == "Deleted" && m.ProductId == model.Id)), Times.Once);
    }

    [Fact]
    public async Task DeleteProduct_ShouldNotAppearInActiveProducts_AfterDeletion()
    {
        var model = BuildProduct("ToDelete", "DEL-002", isActive: true);
        await _sut.CreateProduct(model);
        await _sut.DeleteProduct(model.Id);

        var activeProducts = await _sut.GetAllActiveProducts();

        Assert.DoesNotContain(activeProducts, p => p.Id == model.Id);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ProductModel BuildProduct(string name, string sku, bool isActive = true) => new()
    {
        Name = name,
        Sku = sku,
        Description = "Test product",
        Price = 9.99m,
        StockQuantity = 10,
        Category = ProductCategory.General,
        IsActive = isActive,
        IsDeleted = false
    };

    public void Dispose() => _context.Dispose();
}
