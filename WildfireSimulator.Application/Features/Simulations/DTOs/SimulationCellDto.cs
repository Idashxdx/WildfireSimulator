using System.Text.Json.Serialization;

namespace WildfireSimulator.Application.Features.Simulations.DTOs;

public class SimulationCellDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("vegetation")]
    public string Vegetation { get; set; } = string.Empty;

    [JsonPropertyName("moisture")]
    public double Moisture { get; set; }

    [JsonPropertyName("elevation")]
    public double Elevation { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("burnProbability")]
    public double BurnProbability { get; set; }

    [JsonPropertyName("ignitionTime")]
    public DateTime? IgnitionTime { get; set; }

    [JsonPropertyName("burnoutTime")]
    public DateTime? BurnoutTime { get; set; }

    [JsonPropertyName("receivedHeat")]
    public double? ReceivedHeat { get; set; }

    [JsonPropertyName("ignitionThreshold")]
    public double? IgnitionThreshold { get; set; }

    [JsonPropertyName("ratio")]
    public double? Ratio { get; set; }

    [JsonPropertyName("windFactor")]
    public double? WindFactor { get; set; }

    [JsonPropertyName("slopeFactor")]
    public double? SlopeFactor { get; set; }

    [JsonPropertyName("distance")]
    public double? Distance { get; set; }

    [JsonPropertyName("diagnosticSourceCount")]
    public int? DiagnosticSourceCount { get; set; }

    [JsonPropertyName("fireStage")]
    public string FireStage { get; set; } = string.Empty;

    [JsonPropertyName("fireIntensity")]
    public double FireIntensity { get; set; }

    [JsonPropertyName("currentFuelLoad")]
    public double CurrentFuelLoad { get; set; }

    [JsonPropertyName("fuelLoad")]
    public double FuelLoad { get; set; }

    [JsonPropertyName("burningElapsedSeconds")]
    public double BurningElapsedSeconds { get; set; }

    [JsonPropertyName("accumulatedHeatJ")]
    public double AccumulatedHeatJ { get; set; }
}