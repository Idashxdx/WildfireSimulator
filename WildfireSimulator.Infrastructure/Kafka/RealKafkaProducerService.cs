using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WildfireSimulator.Application.Interfaces;

namespace WildfireSimulator.Infrastructure.Kafka;

public class RealKafkaProducerService : IKafkaProducerService
{
    private readonly ILogger<RealKafkaProducerService> _logger;
    private readonly KafkaConfig _config;
    private readonly IProducer<Null, string> _producer;
    private bool _disposed = false;
    
    public RealKafkaProducerService(
        ILogger<RealKafkaProducerService> logger,
        IOptions<KafkaConfig> config)
    {
        _logger = logger;
        _config = config.Value;
        
        var producerConfig = _config.GetProducerConfig();
        
        _producer = new ProducerBuilder<Null, string>(producerConfig)
            .SetErrorHandler((_, error) =>
            {
                _logger.LogError("Kafka producer error: {Error}", error.Reason);
            })
            .SetLogHandler((_, logMessage) =>
            {
                if (logMessage.Level >= SyslogLevel.Warning)
                {
                    _logger.LogDebug("Kafka log: {Message}", logMessage.Message);
                }
            })
            .Build();
            
        _logger.LogInformation("Real Kafka producer initialized for {BootstrapServers}", _config.BootstrapServers);
    }
    
    public async Task ProduceAsync<T>(string topic, T message) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
            
            var kafkaMessage = new Message<Null, string>
            {
                Value = json,
                Timestamp = new Timestamp(DateTime.UtcNow)
            };
            
            var result = await _producer.ProduceAsync(topic, kafkaMessage);
            
            _logger.LogDebug(
                "Kafka message sent to {Topic} [Partition: {Partition}, Offset: {Offset}]",
                result.Topic, result.Partition, result.Offset.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to Kafka topic {Topic}", topic);
            throw;
        }
    }
    
    public async Task ProduceFireEventAsync(FireEvent fireEvent)
    {
        await ProduceAsync(_config.TopicConfig.FireEvents, fireEvent);
    }
    
    public async Task ProduceMetricsAsync(FireMetricsEvent metricsEvent)
    {
        await ProduceAsync(_config.TopicConfig.Metrics, metricsEvent);
    }
    
    public async Task ProduceSimulationCommandAsync(SimulationCommand command)
    {
        await ProduceAsync(_config.TopicConfig.SimulationCommands, command);
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _producer?.Flush(TimeSpan.FromSeconds(10));
            _producer?.Dispose();
            _disposed = true;
        }
    }
    
    ~RealKafkaProducerService()
    {
        Dispose();
    }
}
