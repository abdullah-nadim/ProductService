using Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using ProductService.Models;

namespace ProductService.Services;

public interface IProductEventPublisher
{
    Task PublishProductCreatedAsync(ProductMessage message);
    Task PublishProductUpdatedAsync(ProductMessage message);
    Task PublishProductDeletedAsync(ProductMessage message);
}

public class ProductEventPublisher : IProductEventPublisher
{
    private readonly IRabbitMQService _rabbitMQ;
    private readonly ILogger<ProductEventPublisher> _logger;

    private const string Exchange = "service.events";

    public ProductEventPublisher(IRabbitMQService rabbitMQ, ILogger<ProductEventPublisher> logger)
    {
        _rabbitMQ = rabbitMQ;
        _logger = logger;
    }

    public async Task PublishProductCreatedAsync(ProductMessage message)
    {
        await _rabbitMQ.PublishMessageAsync("product.created", message, Exchange);
        _logger.LogInformation("Published product.created for ProductId: {Id}", message.ProductId);
    }

    public async Task PublishProductUpdatedAsync(ProductMessage message)
    {
        await _rabbitMQ.PublishMessageAsync("product.updated", message, Exchange);
        _logger.LogInformation("Published product.updated for ProductId: {Id}", message.ProductId);
    }

    public async Task PublishProductDeletedAsync(ProductMessage message)
    {
        await _rabbitMQ.PublishMessageAsync("product.deleted", message, Exchange);
        _logger.LogInformation("Published product.deleted for ProductId: {Id}", message.ProductId);
    }
}
