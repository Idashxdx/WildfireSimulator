using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WildfireSimulator.Client.Models;

public class StepResultDto
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

    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}