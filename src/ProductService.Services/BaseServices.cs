using Infrastructure.Redis.Configuration;
using Infrastructure.Redis.Services;
using Microsoft.Extensions.Options;
using ProductService.Repository;

namespace ProductService.Services;

// Base class for all service classes in this microservice.
// Provides access to DatabaseContext and optionally Redis cache.
public abstract class BaseServices
{
    protected readonly DatabaseContext _Context;
    protected readonly ICacheService? _cacheService;
    protected readonly CacheExpirationOptions? _cacheExpiration;

    protected BaseServices(DatabaseContext context)
    {
        _Context = context;
    }

    protected BaseServices(DatabaseContext context, ICacheService cacheService, IOptions<CacheExpirationOptions> cacheExpiration)
    {
        _Context = context;
        _cacheService = cacheService;
        _cacheExpiration = cacheExpiration.Value;
    }
}
