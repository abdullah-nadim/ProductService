namespace Infrastructure.Messaging;

public interface IRabbitMQService
{
    Task PublishMessageAsync<T>(string routingKey, T message, string exchange = "service.events");
    Task SubscribeAsync<T>(string queueName, Func<T, Task> messageHandler, CancellationToken cancellationToken = default);
}
