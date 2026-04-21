using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WildfireSimulator.Client.Models;

public class SimulationGraphResponseDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("graph")]
    public SimulationGraphDto? Graph { get; set; }
}

public class SimulationGraphDto
{
    [JsonPropertyName("simulationId")]
    public Guid SimulationId { get; set; }

    [JsonPropertyName("simulationName")]
    public string SimulationName { get; set; } = string.Empty;

    [JsonPropertyName("graphType")]
    public GraphType GraphType { get; set; } = GraphType.Grid;

    [JsonPropertyName("graphScaleType")]
    public GraphScaleType? GraphScaleType { get; set; }

    [JsonPropertyName("layoutHint")]
    public string LayoutHint { get; set; } = "grid";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("stepDurationSeconds")]
    public int StepDurationSeconds { get; set; }

    [JsonPropertyName("nodes")]
    public List<SimulationGraphNodeDto> Nodes { get; set; } = new();

    [JsonPropertyName("edges")]
    public List<SimulationGraphEdgeDto> Edges { get; set; } = new();
}

public class SimulationGraphNodeDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("renderX")]
    public double RenderX { get; set; }

    [JsonPropertyName("renderY")]
    public double RenderY { get; set; }

    [JsonPropertyName("groupKey")]
    public string GroupKey { get; set; } = string.Empty;

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

    public bool IsSelectedIgnition { get; set; }

    public bool IsBurning => State == "Burning";
    public bool IsBurned => State == "Burned";
    public bool IsNormal => State == "Normal";
}

public class SimulationGraphEdgeDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("fromCellId")]
    public Guid FromCellId { get; set; }

    [JsonPropertyName("fromX")]
    public int FromX { get; set; }

    [JsonPropertyName("fromY")]
    public int FromY { get; set; }

    [JsonPropertyName("fromRenderX")]
    public double FromRenderX { get; set; }

    [JsonPropertyName("fromRenderY")]
    public double FromRenderY { get; set; }

    [JsonPropertyName("toCellId")]
    public Guid ToCellId { get; set; }

    [JsonPropertyName("toX")]
    public int ToX { get; set; }

    [JsonPropertyName("toY")]
    public int ToY { get; set; }

    [JsonPropertyName("toRenderX")]
    public double ToRenderX { get; set; }

    [JsonPropertyName("toRenderY")]
    public double ToRenderY { get; set; }

    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    [JsonPropertyName("slope")]
    public double Slope { get; set; }

    [JsonPropertyName("fireSpreadModifier")]
    public double FireSpreadModifier { get; set; }

    [JsonPropertyName("isCorridor")]
    public bool IsCorridor { get; set; }
}