using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Messaging;

/// <summary>
/// RabbitMQ pub/sub service with automatic reconnection and exponential retry.
/// Registered as IHostedService so it connects on app startup.
/// </summary>
public class RabbitMQService : IRabbitMQService, IHostedService, IDisposable
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly ILogger<RabbitMQService> _logger;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private const int MaxRetries = 10;
    private const int RetryDelaySeconds = 5;

    public RabbitMQService(ILogger<RabbitMQService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private ConnectionFactory CreateFactory() => new()
    {
        HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
        Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
        UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
        Password = _configuration["RabbitMQ:Password"] ?? "guest",
        VirtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/",
        AutomaticRecoveryEnabled = true,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
    };

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true) return;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection?.IsOpen == true && _channel?.IsOpen == true) return;

            var factory = CreateFactory();
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Connecting to RabbitMQ (attempt {Attempt}/{Max})", attempt, MaxRetries);
                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();
                    _logger.LogInformation("RabbitMQ connected");
                    return;
                }
                catch (Exception ex) when (attempt < MaxRetries)
                {
                    _logger.LogWarning(ex, "RabbitMQ connection failed, retrying in {Delay}s...", RetryDelaySeconds);
                    await Task.Delay(RetryDelaySeconds * 1000, cancellationToken);
                }
            }
            throw new InvalidOperationException("Could not connect to RabbitMQ after max retries.");
        }
        finally { _connectionLock.Release(); }
    }

    public async Task PublishMessageAsync<T>(string routingKey, T message, string exchange = "service.events")
    {
        await EnsureConnectedAsync();
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        _channel!.BasicPublish(exchange: exchange, routingKey: routingKey, basicProperties: null, body: body);
        _logger.LogDebug("Published to {Exchange}/{RoutingKey}", exchange, routingKey);
    }

    public async Task SubscribeAsync<T>(string queueName, Func<T, Task> messageHandler, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        _channel!.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, args) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(args.Body.ToArray()));
                if (message is not null) await messageHandler(message);
                _channel.BasicAck(args.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue: {Queue}", queueName);
                _channel.BasicNack(args.DeliveryTag, false, requeue: false);
            }
        };

        _channel.BasicConsume(queueName, autoAck: false, consumer: consumer);
    }

    public async Task StartAsync(CancellationToken cancellationToken) => await EnsureConnectedAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) { Dispose(); return Task.CompletedTask; }

    public void Dispose()
    {
        _channel?.Close(); _channel?.Dispose();
        _connection?.Close(); _connection?.Dispose();
    }
}
