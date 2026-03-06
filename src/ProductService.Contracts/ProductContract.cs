using System.ComponentModel.DataAnnotations;
using ProductService.Models;

namespace ProductService.Contracts;

public class ProductContract : BaseContract<ProductContract, ProductModel>
{
    public ProductContract()
    {
        Name = Sku = string.Empty;
    }

    public long Id { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "StockQuantity cannot be negative")]
    public int StockQuantity { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Sku { get; set; }

    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
}
