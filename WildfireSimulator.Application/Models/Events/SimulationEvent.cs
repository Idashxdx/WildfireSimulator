using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Models.Events;

public abstract class SimulationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public Guid SimulationId { get; set; }
    public int Step { get; set; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string EventType { get; protected set; } = string.Empty;
}

public class CellIgnitedEvent : SimulationEvent
{
    public Guid CellId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public double IgnitionProbability { get; set; }
    public VegetationType Vegetation { get; set; }
    
    public CellIgnitedEvent()
    {
        EventType = "CellIgnited";
    }
}

public class CellBurnedOutEvent : SimulationEvent
{
    public Guid CellId { get; set; }
    public TimeSpan BurnDuration { get; set; }
    
    public CellBurnedOutEvent()
    {
        EventType = "CellBurnedOut";
    }
}

public class FireSpreadEvent : SimulationEvent
{
    public Guid FromCellId { get; set; }
    public Guid ToCellId { get; set; }
    public double SpreadProbability { get; set; }
    public bool DidSpread { get; set; }
    
    public FireSpreadEvent()
    {
        EventType = "FireSpread";
    }
}

public class SimulationStepCompletedEvent : SimulationEvent
{
    public int BurningCellsCount { get; set; }
    public int BurnedCellsCount { get; set; }
    public double FireArea { get; set; }
    public double SpreadSpeed { get; set; }
    
    public SimulationStepCompletedEvent()
    {
        EventType = "SimulationStepCompleted";
    }
}

public class WeatherChangedEvent : SimulationEvent
{
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double WindSpeed { get; set; }
    public WindDirection WindDirection { get; set; }
    
    public WeatherChangedEvent()
    {
        EventType = "WeatherChanged";
    }
}
