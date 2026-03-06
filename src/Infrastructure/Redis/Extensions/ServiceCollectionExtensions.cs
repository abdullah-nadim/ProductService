using Infrastructure.Redis.Configuration;
using Infrastructure.Redis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Redis.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Redis cache with a single shared ConnectionMultiplexer (singleton).
    /// Reads config from "Redis" section in appsettings.
    /// </summary>
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RedisOptions>(configuration.GetSection("Redis"));
        services.Configure<CacheExpirationOptions>(configuration.GetSection("Redis:CacheExpiration"));

        // One multiplexer for the whole app — StackExchange.Redis manages the connection pool
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            var config = BuildConfig(options);
            var multiplexer = ConnectionMultiplexer.Connect(config);
            var logger = sp.GetService<ILogger<IConnectionMultiplexer>>();

            multiplexer.ConnectionFailed += (_, e) =>
                logger?.LogWarning("Redis connection failed: {Type} on {Endpoint}", e.FailureType, e.EndPoint);
            multiplexer.ConnectionRestored += (_, e) =>
                logger?.LogInformation("Redis connection restored: {Endpoint}", e.EndPoint);

            return multiplexer;
        });

        services.AddStackExchangeRedisCache(opt =>
        {
            var redisOptions = configuration.GetSection("Redis").Get<RedisOptions>() ?? new();
            opt.Configuration = BuildConnectionString(redisOptions);
            opt.InstanceName = redisOptions.InstanceName;
        });

        services.AddScoped<ICacheService, RedisCacheService>();
        return services;
    }

    private static ConfigurationOptions BuildConfig(RedisOptions options)
    {
        var config = ConfigurationOptions.Parse(BuildConnectionString(options));
        config.ConnectTimeout = options.ConnectTimeout;
        config.SyncTimeout = options.SyncTimeout;
        config.AsyncTimeout = options.SyncTimeout;
        config.AbortOnConnectFail = options.AbortOnConnectFail;
        config.ConnectRetry = options.RetryOnConnectionFailure ? 3 : 0;
        config.KeepAlive = 60;
        config.ReconnectRetryPolicy = new ExponentialRetry(100, 1000);
        return config;
    }

    private static string BuildConnectionString(RedisOptions options)
    {
        var cs = options.ConnectionString;
        if (!string.IsNullOrEmpty(options.Password) && !cs.Contains("password=", StringComparison.OrdinalIgnoreCase))
            cs += $",password={options.Password}";
        if (options.Database > 0 && !cs.Contains("defaultDatabase=", StringComparison.OrdinalIgnoreCase))
            cs += $",defaultDatabase={options.Database}";
        return cs;
    }
}
