using System;
using System.Text.Json.Serialization;

namespace WildfireSimulator.Client.Models;

public class GraphCellDto
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

    [JsonPropertyName("isIgnitable")]
    public bool IsIgnitable { get; set; } = true;

    public bool IsSelectedIgnition { get; set; }

    public bool IsBurning => State == "Burning";
    public bool IsBurned => State == "Burned";
    public bool IsNormal => State == "Normal";

    [JsonPropertyName("precipitationIntensity")]
    public double PrecipitationIntensity { get; set; }

    [JsonPropertyName("isInPrecipitationFront")]
    public bool IsInPrecipitationFront { get; set; }
}