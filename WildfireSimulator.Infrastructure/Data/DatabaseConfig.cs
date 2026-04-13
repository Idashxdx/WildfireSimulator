namespace WildfireSimulator.Infrastructure.Data;

public class DatabaseConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public bool EnableSensitiveDataLogging { get; set; }
    public bool EnableDetailedErrors { get; set; }
}
