using System.Text.Json.Serialization;
using WildfireSimulator.Application.Interfaces;
using WildfireSimulator.Application.Models.Events;

namespace WildfireSimulator.Application.Features.Simulations.DTOs;

public class SimulationStepDto
{
    [JsonPropertyName("step")]
    public int Step { get; set; }
    
    [JsonPropertyName("simulationId")]
    public Guid SimulationId { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("burningCellsCount")]
    public int BurningCellsCount { get; set; }
    
    [JsonPropertyName("burnedCellsCount")]
    public int BurnedCellsCount { get; set; }
    
    [JsonPropertyName("newlyIgnitedCells")]
    public int NewlyIgnitedCells { get; set; }
    
    [JsonPropertyName("totalCellsAffected")]
    public int TotalCellsAffected { get; set; }
    
    [JsonPropertyName("fireArea")]
    public double FireArea { get; set; }
    
    [JsonPropertyName("spreadSpeed")]
    public double SpreadSpeed { get; set; }
    
    [JsonPropertyName("events")]
    public List<SimulationEventDto> Events { get; set; } = new();
    
    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    
    public static SimulationStepDto FromResult(SimulationStepResult result)
    {
        return new SimulationStepDto
        {
            Step = result.Step,
            SimulationId = result.SimulationId,
            Timestamp = result.Timestamp,
            BurningCellsCount = result.BurningCellsCount,
            BurnedCellsCount = result.BurnedCellsCount,
            NewlyIgnitedCells = result.NewlyIgnitedCells,
            TotalCellsAffected = result.TotalCellsAffected,
            FireArea = result.FireArea,
            SpreadSpeed = result.SpreadSpeed,
            Events = result.Events.Select(SimulationEventDto.FromEvent).ToList(),
            IsSuccess = result.IsSuccess,
            Error = result.Error
        };
    }
}

public class SimulationEventDto
{
    [JsonPropertyName("eventId")]
    public Guid EventId { get; set; }
    
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;
    
    [JsonPropertyName("simulationId")]
    public Guid SimulationId { get; set; }
    
    [JsonPropertyName("step")]
    public int Step { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();
    
    public static SimulationEventDto FromEvent(SimulationEvent simulationEvent)
    {
        var dto = new SimulationEventDto
        {
            EventId = simulationEvent.EventId,
            EventType = simulationEvent.EventType,
            SimulationId = simulationEvent.SimulationId,
            Step = simulationEvent.Step,
            Timestamp = simulationEvent.Timestamp
        };
        
        switch (simulationEvent)
        {
            case CellIgnitedEvent ignited:
                dto.Data["cellId"] = ignited.CellId;
                dto.Data["x"] = ignited.X;
                dto.Data["y"] = ignited.Y;
                dto.Data["ignitionProbability"] = ignited.IgnitionProbability;
                dto.Data["vegetation"] = ignited.Vegetation.ToString();
                break;
                
            case CellBurnedOutEvent burnedOut:
                dto.Data["cellId"] = burnedOut.CellId;
                dto.Data["burnDuration"] = burnedOut.BurnDuration.TotalSeconds;
                break;
                
            case FireSpreadEvent spread:
                dto.Data["fromCellId"] = spread.FromCellId;
                dto.Data["toCellId"] = spread.ToCellId;
                dto.Data["spreadProbability"] = spread.SpreadProbability;
                dto.Data["didSpread"] = spread.DidSpread;
                break;
                
            case SimulationStepCompletedEvent stepCompleted:
                dto.Data["burningCellsCount"] = stepCompleted.BurningCellsCount;
                dto.Data["burnedCellsCount"] = stepCompleted.BurnedCellsCount;
                dto.Data["fireArea"] = stepCompleted.FireArea;
                dto.Data["spreadSpeed"] = stepCompleted.SpreadSpeed;
                break;
                
            case WeatherChangedEvent weatherChanged:
                dto.Data["temperature"] = weatherChanged.Temperature;
                dto.Data["humidity"] = weatherChanged.Humidity;
                dto.Data["windSpeed"] = weatherChanged.WindSpeed;
                dto.Data["windDirection"] = weatherChanged.WindDirection.ToString();
                break;
        }
        
        return dto;
    }
}
