using System;
using System.Collections.Generic;

namespace WildfireSimulator.Client.Models;

public class SimulationCreationResult
{
    public GraphType GraphType { get; set; } = GraphType.Grid;
    public GraphScaleType? GraphScaleType { get; set; }

    public string SimulationName { get; set; } = string.Empty;

    public int GridWidth { get; set; } = 20;
    public int GridHeight { get; set; } = 20;
    public int InitialFireCells { get; set; } = 3;

    public double MoistureMin { get; set; } = 0.3;
    public double MoistureMax { get; set; } = 0.7;
    public double ElevationVariation { get; set; } = 50.0;

    public int SimulationSteps { get; set; } = 100;
    public int StepDurationSeconds { get; set; } = 900;

    public double Temperature { get; set; } = 25.0;
    public double Humidity { get; set; } = 40.0;
    public double WindSpeed { get; set; } = 5.0;
    public double WindDirection { get; set; } = 45.0;
    public double Precipitation { get; set; } = 0.0;

    public int? RandomSeed { get; set; }

    public string? SelectedDemoPreset { get; set; }

    public PreparedGridMapDto? PreparedMap { get; set; }

    public bool UsePreparedMap => PreparedMap != null;

    // Временно оставляем старые поля, чтобы не ломать остальной клиент (связанный с графом),
    public MapCreationMode SelectedMapCreationMode { get; set; } = MapCreationMode.Random;
    public MapScenarioType? SelectedScenarioType { get; set; }
    public ClusteredScenarioType? SelectedClusteredScenarioType { get; set; }

    public double MapNoiseStrength { get; set; } = 0.08;
    public double MapDrynessFactor { get; set; } = 1.0;
    public double ReliefStrengthFactor { get; set; } = 1.0;
    public double FuelDensityFactor { get; set; } = 1.0;

    public List<MapRegionObjectDto> MapRegionObjects { get; set; } = new();
    public List<(int VegetationType, double Probability)> VegetationDistributions { get; set; } = new();

    public ClusteredGraphBlueprintDto? ClusteredBlueprint { get; set; }
}