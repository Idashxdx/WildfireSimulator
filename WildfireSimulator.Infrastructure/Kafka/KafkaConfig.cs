using Confluent.Kafka;

namespace WildfireSimulator.Infrastructure.Kafka;

public class KafkaConfig
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    
    public class Topics
    {
        public string FireEvents { get; set; } = "fire-events";
        public string Metrics { get; set; } = "fire-metrics";
        public string SimulationCommands { get; set; } = "simulation-commands";
    }
    
    public Topics TopicConfig { get; set; } = new();
    
    public string ConsumerGroupId { get; set; } = "wildfire-simulator-group";
    
    public ProducerConfig GetProducerConfig()
    {
        return new ProducerConfig
        {
            BootstrapServers = BootstrapServers,
            Acks = Acks.All,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100,
            LingerMs = 5,
            BatchSize = 32768
        };
    }
    
    public ConsumerConfig GetConsumerConfig()
    {
        return new ConsumerConfig
        {
            BootstrapServers = BootstrapServers,
            GroupId = ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = false,
            StatisticsIntervalMs = 5000,
            SessionTimeoutMs = 6000,
            MaxPollIntervalMs = 300000
        };
    }
}
