using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace WildfireSimulator.Infrastructure.Kafka;

public static class KafkaDependencyInjection
{
    public static IServiceCollection AddKafkaServices(this IServiceCollection services, IConfiguration configuration)
    {
        var kafkaConfig = new KafkaConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            ConsumerGroupId = configuration["Kafka:ConsumerGroupId"] ?? "wildfire-simulator-group",
            TopicConfig = new KafkaConfig.Topics
            {
                FireEvents = configuration["Kafka:TopicConfig:FireEvents"] ?? "fire-events",
                Metrics = configuration["Kafka:TopicConfig:Metrics"] ?? "fire-metrics",
                SimulationCommands = configuration["Kafka:TopicConfig:SimulationCommands"] ?? "simulation-commands"
            }
        };
        
        services.AddSingleton(kafkaConfig);
        
        return services;
    }
}
