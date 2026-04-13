using System.Text.Json.Serialization;
using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Features.Simulations.DTOs;

public class CreateSimulationDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("gridWidth")]
    public int GridWidth { get; set; } = 50;
    
    [JsonPropertyName("gridHeight")]
    public int GridHeight { get; set; } = 50;
    
    [JsonPropertyName("graphType")]
    public GraphType GraphType { get; set; } = GraphType.Grid;
    
    [JsonPropertyName("initialMoistureMin")]
    public double InitialMoistureMin { get; set; } = 0.2;
    
    [JsonPropertyName("initialMoistureMax")]
    public double InitialMoistureMax { get; set; } = 0.8;
    
    [JsonPropertyName("elevationVariation")]
    public double ElevationVariation { get; set; } = 100.0;
    
    [JsonPropertyName("initialFireCellsCount")]
    public int InitialFireCellsCount { get; set; } = 1;
    
    [JsonPropertyName("simulationSteps")]
    public int SimulationSteps { get; set; } = 100;
    
    [JsonPropertyName("stepDurationSeconds")]
    public int StepDurationSeconds { get; set; } = 60;
}