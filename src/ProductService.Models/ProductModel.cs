using Core.Models;

namespace ProductService.Models;

public class ProductModel : IAuditableEntity
{
    public ProductModel()
    {
        Name = Sku = string.Empty;
        IsActive = true;
        IsDeleted = false;
        Category = ProductCategory.General;
    }

    public ProductModel Update(ProductModel updated)
    {
        Name = updated.Name;
        Description = updated.Description;
        Price = updated.Price;
        StockQuantity = updated.StockQuantity;
        Sku = updated.Sku;
        Category = updated.Category;
        return this;
    }

    public long Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string Sku { get; set; }
    public ProductCategory Category { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    // IAuditableEntity — auto-set by DatabaseContext on SaveChanges
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
}
