using Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using ProductService.Models;

namespace ProductService.Services;

public interface IProductEventSubscriber
{
    Task StartAsync(CancellationToken cancellationToken);
}

public class ProductEventSubscriber : IProductEventSubscriber
{
    private readonly IRabbitMQService _rabbitMQ;
    private readonly ILogger<ProductEventSubscriber> _logger;

    public ProductEventSubscriber(IRabbitMQService rabbitMQ, ILogger<ProductEventSubscriber> logger)
    {
        _rabbitMQ = rabbitMQ;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Subscribe to events from other services here.
        // Example: listen for inventory updates from another microservice.
        await _rabbitMQ.SubscribeAsync<ProductMessage>(
            "product-service.inventory-updates",
            HandleInventoryUpdateAsync,
            cancellationToken);

        _logger.LogInformation("ProductEventSubscriber started");
    }

    private async Task HandleInventoryUpdateAsync(ProductMessage message)
    {
        _logger.LogInformation("Received inventory update for ProductId: {Id}", message.ProductId);
        // Handle cross-service event here
        await Task.CompletedTask;
    }
}
