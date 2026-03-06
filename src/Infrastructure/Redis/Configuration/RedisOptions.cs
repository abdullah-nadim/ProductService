namespace Infrastructure.Redis.Configuration;

public class RedisOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string InstanceName { get; set; } = "app";
    public int Database { get; set; } = 0;
    public int ConnectTimeout { get; set; } = 5000;
    public int SyncTimeout { get; set; } = 5000;
    public bool EnableSsl { get; set; } = false;
    public string? Password { get; set; }
    public bool AbortOnConnectFail { get; set; } = false;
    public bool RetryOnConnectionFailure { get; set; } = true;
}
