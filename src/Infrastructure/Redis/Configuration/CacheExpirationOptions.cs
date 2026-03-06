namespace Infrastructure.Redis.Configuration;

public class CacheExpirationOptions
{
    /// <summary>Frequently changing data — permissions, active sessions (default: 10 min)</summary>
    public TimeSpan ShortLived { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Moderately changing data — entity lists, reference lookups (default: 30 min)</summary>
    public TimeSpan MediumLived { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Rarely changing data — config, enums, categories (default: 2 hrs)</summary>
    public TimeSpan LongLived { get; set; } = TimeSpan.FromHours(2);
}
