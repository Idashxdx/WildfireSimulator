using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WildfireSimulator.Infrastructure.Data;
using WildfireSimulator.Application.Interfaces;

namespace WildfireSimulator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HealthController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IKafkaProducerService _kafkaProducer;

    public HealthController(
        ApplicationDbContext context,
        ILogger<HealthController> logger,
        IConfiguration configuration,
        IKafkaProducerService kafkaProducer)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _kafkaProducer = kafkaProducer;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var health = new
        {
            status = "Checking...",
            timestamp = DateTime.UtcNow,
            services = new Dictionary<string, object>()
        };

        try
        {
            var dbCheck = await CheckDatabaseAsync();
            health.services["database"] = dbCheck;

            var kafkaCheck = CheckKafka();
            health.services["kafka"] = kafkaCheck;

            health.services["api"] = new
            {
                status = "Running",
                port = _configuration["ASPNETCORE_URLS"] ?? "http://localhost:5198",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
            };

            var dbStatus = ((dynamic)dbCheck).status.ToString();
            var kafkaStatus = ((dynamic)kafkaCheck).status.ToString();
            var allHealthy = dbStatus == "Healthy" && kafkaStatus == "Available";

            return Ok(new
            {
                status = allHealthy ? "Healthy" : "Degraded",
                timestamp = DateTime.UtcNow,
                uptime = GetUptime(),
                version = GetVersion(),
                services = health.services
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(500, new
            {
                status = "Unhealthy",
                timestamp = DateTime.UtcNow,
                error = ex.Message,
                services = health.services
            });
        }
    }

    [HttpGet("database")]
    public async Task<IActionResult> CheckDatabase()
    {
        var result = await CheckDatabaseAsync();
        var status = ((dynamic)result).status.ToString();
        return status == "Healthy"
            ? Ok(result)
            : StatusCode(503, result);
    }

    [HttpGet("kafka")]
    public IActionResult CheckKafkaEndpoint()
    {
        var result = CheckKafka();
        var status = ((dynamic)result).status.ToString();
        return status == "Available"
            ? Ok(result)
            : StatusCode(503, result);
    }

    [HttpGet("simulations/count")]
    public async Task<IActionResult> GetSimulationsCount()
    {
        try
        {
            var count = await _context.Simulations.CountAsync();
            return Ok(new { count = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get simulations count");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private async Task<object> CheckDatabaseAsync()
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync();

            if (!canConnect)
            {
                return new
                {
                    status = "Unhealthy",
                    message = "Cannot connect to database",
                    timestamp = DateTime.UtcNow
                };
            }

            var simulationsCount = await _context.Simulations.CountAsync();

            return new
            {
                status = "Healthy",
                message = "Database is accessible",
                timestamp = DateTime.UtcNow,
                details = new
                {
                    canConnect = true,
                    simulationsCount = simulationsCount,
                    database = _context.Database.ProviderName
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return new
            {
                status = "Unhealthy",
                message = ex.Message,
                timestamp = DateTime.UtcNow,
                details = new
                {
                    canConnect = false,
                    error = ex.GetType().Name
                }
            };
        }
    }

    private object CheckKafka()
    {
        try
        {
            var kafkaConfig = _configuration.GetSection("Kafka");
            var bootstrapServers = kafkaConfig["BootstrapServers"] ?? "localhost:9092";

            string producerType = _kafkaProducer.GetType().Name;
            bool isRealProducer = producerType.Contains("Real") || producerType.Contains("RealKafkaProducerService");

            return new
            {
                status = "Available",
                message = isRealProducer ? "Kafka connected with real producer" : "Kafka configured",
                timestamp = DateTime.UtcNow,
                details = new
                {
                    bootstrapServers = bootstrapServers,
                    topics = new[]
{
    "fire-events",
    "fire-metrics",
    "fire-moving-averages",
    "fire-trends",
    "fire-anomalies",
    "fire-forecast"
},
                    producerType = producerType,
                    usingRealProducer = isRealProducer
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka health check failed");
            return new
            {
                status = "Unavailable",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            };
        }
    }

    private TimeSpan GetUptime()
    {
        try
        {
            return DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    private string GetVersion()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "1.0.0";

            var versionAttribute = assembly.GetCustomAttributes(
                typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .FirstOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute;

            var informationalVersion = versionAttribute?.InformationalVersion ?? version;

            return $"{version} ({informationalVersion})";
        }
        catch
        {
            return "1.0.0 (unknown)";
        }
    }
}
