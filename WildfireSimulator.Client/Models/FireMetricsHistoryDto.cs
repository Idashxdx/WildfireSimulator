using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WildfireSimulator.Client.Models;

public class FireMetricsHistoryDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("simulationId")]
    public Guid SimulationId { get; set; }

    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("burningCellsCount")]
    public int BurningCellsCount { get; set; }

    [JsonPropertyName("burnedCellsCount")]
    public int BurnedCellsCount { get; set; }

    [JsonPropertyName("totalCellsAffected")]
    public int TotalCellsAffected { get; set; }

    [JsonPropertyName("fireSpreadSpeed")]
    public double FireSpreadSpeed { get; set; }

    [JsonPropertyName("averageTemperature")]
    public double AverageTemperature { get; set; }

    [JsonPropertyName("averageWindSpeed")]
    public double AverageWindSpeed { get; set; }

    [JsonPropertyName("fireArea")]
    public double FireArea { get; set; }
}

public class FireMetricsHistoryResponseDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("simulationId")]
    public Guid SimulationId { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("metrics")]
    public List<FireMetricsHistoryDto> Metrics { get; set; } = new();
}