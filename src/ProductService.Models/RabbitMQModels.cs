namespace ProductService.Models;

public class ProductMessage
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // Created, Updated, Deleted
    public DateTime OccurredAt { get; set; }
}

/// <summary>
/// Consumed from the order-service queue when an order is placed.
/// ProductService uses this to decrement stock (Event-Carried State Transfer).
/// </summary>
public class OrderPlacedMessage
{
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime OccurredAt { get; set; }
}
