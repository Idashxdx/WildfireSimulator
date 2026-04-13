using System.Threading.Tasks;

namespace WildfireSimulator.Application.Interfaces;

public interface IKafkaProducerService
{
    Task ProduceAsync<T>(string topic, T message) where T : class;
    Task ProduceFireEventAsync(FireEvent fireEvent);
    Task ProduceMetricsAsync(FireMetricsEvent metricsEvent);
    Task ProduceSimulationCommandAsync(SimulationCommand command);
}

public abstract class KafkaEvent
{
    public string EventId { get; set; } = System.Guid.NewGuid().ToString();
    public string EventType { get; set; } = string.Empty;
    public System.DateTime Timestamp { get; set; } = System.DateTime.UtcNow;
    public System.Guid SimulationId { get; set; }
    public int Step { get; set; }
}

public class FireEvent : KafkaEvent
{
    public string CellId { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public string Vegetation { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public double IgnitionProbability { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double WindSpeed { get; set; }
    public string WindDirection { get; set; } = string.Empty;
}

public class FireMetricsEvent : KafkaEvent
{
    public int BurningCellsCount { get; set; }
    public int BurnedCellsCount { get; set; }
    public int TotalCellsAffected { get; set; }
    public double FireArea { get; set; }
    public double SpreadSpeed { get; set; }
    public double Perimeter { get; set; }
    public double Intensity { get; set; }
}

public class SimulationCommand : KafkaEvent
{
    public string CommandType { get; set; } = string.Empty;
    public System.Collections.Generic.Dictionary<string, object> Parameters { get; set; } = new();
}
