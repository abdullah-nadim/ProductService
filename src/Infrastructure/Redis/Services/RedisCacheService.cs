using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Infrastructure.Redis.Services;

/// <summary>
/// Redis-backed cache with availability tracking and graceful fallback.
/// If Redis goes down, all operations fail silently — service continues from DB.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Availability tracking — avoids hammering a dead Redis on every request
    private volatile bool _isAvailable = true;
    private DateTime _lastCheck = DateTime.MinValue;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _opTimeout = TimeSpan.FromMilliseconds(500);

    public RedisCacheService(
        IDistributedCache distributedCache,
        IConnectionMultiplexer multiplexer,
        ILogger<RedisCacheService> logger)
    {
        _distributedCache = distributedCache;
        _multiplexer = multiplexer;
        _logger = logger;
    }

    private bool IsAvailable()
    {
        if (DateTime.UtcNow - _lastCheck < _checkInterval)
            return _isAvailable;

        _lastCheck = DateTime.UtcNow;
        try
        {
            _isAvailable = _multiplexer.IsConnected && _multiplexer.GetDatabase() != null;
        }
        catch { _isAvailable = false; }

        return _isAvailable;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (!IsAvailable()) return null;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_opTimeout);
            var value = await _distributedCache.GetStringAsync(key, cts.Token);
            return string.IsNullOrEmpty(value) ? null : JsonSerializer.Deserialize<T>(value, _jsonOptions);
        }
        catch (OperationCanceledException) { _isAvailable = false; return null; }
        catch (RedisConnectionException ex) { _isAvailable = false; _logger.LogWarning(ex, "Redis GET failed: {Key}", key); return null; }
        catch (Exception ex) { _logger.LogError(ex, "Cache GET error: {Key}", key); return null; }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        if (!IsAvailable()) return;
        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(30)
            };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_opTimeout);
            await _distributedCache.SetStringAsync(key, json, options, cts.Token);
        }
        catch (OperationCanceledException) { _isAvailable = false; }
        catch (RedisConnectionException ex) { _isAvailable = false; _logger.LogWarning(ex, "Redis SET failed: {Key}", key); }
        catch (Exception ex) { _logger.LogError(ex, "Cache SET error: {Key}", key); }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable()) return;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_opTimeout);
            await _distributedCache.RemoveAsync(key, cts.Token);
        }
        catch (OperationCanceledException) { _isAvailable = false; }
        catch (RedisConnectionException ex) { _isAvailable = false; _logger.LogWarning(ex, "Redis REMOVE failed: {Key}", key); }
        catch (Exception ex) { _logger.LogError(ex, "Cache REMOVE error: {Key}", key); }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable()) return;
        try
        {
            var server = _multiplexer.GetServer(_multiplexer.GetEndPoints().First());
            var database = _multiplexer.GetDatabase();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            await foreach (var key in server.KeysAsync(pattern: pattern).WithCancellation(cts.Token))
            {
                try { await database.KeyDeleteAsync(key); }
                catch (Exception ex) { _logger.LogDebug(ex, "Failed to delete key {Key}", key); }
            }
        }
        catch (OperationCanceledException) { _isAvailable = false; }
        catch (RedisConnectionException ex) { _isAvailable = false; _logger.LogWarning(ex, "Redis pattern removal failed: {Pattern}", pattern); }
        catch (Exception ex) { _logger.LogError(ex, "Cache pattern removal error: {Pattern}", pattern); }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable()) return false;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_opTimeout);
            var value = await _distributedCache.GetStringAsync(key, cts.Token);
            return !string.IsNullOrEmpty(value);
        }
        catch { return false; }
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        if (!IsAvailable())
            return await factory();

        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null) return cached;

        var value = await factory();
        if (value is not null)
        {
            // Fire-and-forget cache write — don't block the response
            _ = Task.Run(() => SetAsync(key, value, expiration, CancellationToken.None));
        }
        return value;
    }

    public async Task RefreshAsync(string key, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable()) return;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_opTimeout);
            var value = await _distributedCache.GetStringAsync(key, cts.Token);
            if (!string.IsNullOrEmpty(value))
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(30)
                };
                await _distributedCache.SetStringAsync(key, value, options, cts.Token);
            }
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Cache refresh failed: {Key}", key); }
    }
}
