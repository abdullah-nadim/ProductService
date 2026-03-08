using Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductService.Models;

namespace ProductService.Services;

/// <summary>
/// Background service (IHostedService) that consumes OrderPlaced events from RabbitMQ
/// and decrements product stock accordingly.
///
/// Demonstrates the Event-Carried State Transfer (ECST) consumer pattern:
/// OrderService publishes → "product-service.order-placed" queue → this service adjusts stock.
///
/// Uses IServiceScopeFactory to safely resolve scoped services (IProductServices)
/// from within a singleton-lifetime hosted service.
/// </summary>
public class ProductEventSubscriber : BackgroundService
{
    private readonly IRabbitMQService _rabbitMQ;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductEventSubscriber> _logger;

    public ProductEventSubscriber(
        IRabbitMQService rabbitMQ,
        IServiceScopeFactory scopeFactory,
        ILogger<ProductEventSubscriber> logger)
    {
        _rabbitMQ = rabbitMQ;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProductEventSubscriber started — listening on 'product-service.order-placed'");

        await _rabbitMQ.SubscribeAsync<OrderPlacedMessage>(
            "product-service.order-placed",
            HandleOrderPlacedAsync,
            stoppingToken);

        // Hold execution open until the host signals shutdown
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleOrderPlacedAsync(OrderPlacedMessage message)
    {
        _logger.LogInformation(
            "Order {OrderId} placed — adjusting stock for product {ProductId} by -{Qty}",
            message.OrderId, message.ProductId, message.Quantity);

        // Create a scope per message: IProductServices is scoped (uses DbContext)
        using var scope = _scopeFactory.CreateScope();
        var productServices = scope.ServiceProvider.GetRequiredService<IProductServices>();
        await productServices.AdjustStockAsync(message.ProductId, -message.Quantity);
    }
}
