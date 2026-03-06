namespace ProductService.Models;

public class ProductMessage
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // Created, Updated, Deleted
    public DateTime OccurredAt { get; set; }
}
