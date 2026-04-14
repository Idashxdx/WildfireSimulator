using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WildfireSimulator.Client.Models;

public enum GraphType
{
    Grid = 0,
    ClusteredGraph = 1,
    RegionClusterGraph = 2
}

public enum VegetationType
{
    Coniferous = 0,
    Deciduous = 1,
    Mixed = 2,
    Grass = 3,
    Shrub = 4,
    Water = 5,
    Bare = 6
}

public enum MapCreationMode
{
    Random = 0,
    Scenario = 1,
    SemiManual = 2
}

public enum MapScenarioType
{
    MixedForest = 0,
    DryConiferousMassif = 1,
    ForestWithRiver = 2,
    ForestWithLake = 3,
    ForestWithFirebreak = 4,
    HillyTerrain = 5,
    WetForestAfterRain = 6
}

public enum MapObjectType
{
    ConiferousArea = 0,
    DeciduousArea = 1,
    MixedForestArea = 2,
    GrassArea = 3,
    ShrubArea = 4,
    WaterBody = 5,
    Firebreak = 6,
    WetZone = 7,
    DryZone = 8,
    Hill = 9,
    Lowland = 10
}

public enum MapObjectShape
{
    Rectangle = 0,
    Ellipse = 1
}

public class SimulationDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("graphType")]
    public GraphType GraphType { get; set; } = GraphType.Grid;

    public string StatusText => Status switch
    {
        0 => "Создана",
        1 => "Запущена",
        2 => "Завершена",
        3 => "Отменена",
        _ => "Неизвестно"
    };

    public string GraphTypeText => GraphType switch
    {
        GraphType.Grid => "Сетка",
        GraphType.ClusteredGraph => "Кластерный граф",
        GraphType.RegionClusterGraph => "Региональный кластерный граф",
        _ => "Неизвестно"
    };

    public bool IsGrid => GraphType == GraphType.Grid;
    public bool IsGraph => GraphType == GraphType.ClusteredGraph || GraphType == GraphType.RegionClusterGraph;
}

public class CreateSimulationDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("gridWidth")]
    public int GridWidth { get; set; } = 20;

    [JsonPropertyName("gridHeight")]
    public int GridHeight { get; set; } = 20;

    [JsonPropertyName("graphType")]
    public int GraphType { get; set; } = 0;

    [JsonPropertyName("initialMoistureMin")]
    public double InitialMoistureMin { get; set; } = 0.3;

    [JsonPropertyName("initialMoistureMax")]
    public double InitialMoistureMax { get; set; } = 0.7;

    [JsonPropertyName("elevationVariation")]
    public double ElevationVariation { get; set; } = 50;

    [JsonPropertyName("initialFireCellsCount")]
    public int InitialFireCellsCount { get; set; } = 3;

    [JsonPropertyName("simulationSteps")]
    public int SimulationSteps { get; set; } = 100;

    [JsonPropertyName("stepDurationSeconds")]
    public int StepDurationSeconds { get; set; } = 900;

    [JsonPropertyName("randomSeed")]
    public int? RandomSeed { get; set; }

    [JsonPropertyName("precipitation")]
    public double Precipitation { get; set; } = 0.0;

    [JsonPropertyName("mapCreationMode")]
    public MapCreationMode MapCreationMode { get; set; } = MapCreationMode.Random;

    [JsonPropertyName("scenarioType")]
    public MapScenarioType? ScenarioType { get; set; }

    [JsonPropertyName("mapNoiseStrength")]
    public double MapNoiseStrength { get; set; } = 0.08;

    [JsonPropertyName("mapRegionObjects")]
    public List<MapRegionObjectDto> MapRegionObjects { get; set; } = new();

    [JsonPropertyName("vegetationDistributions")]
    public List<VegetationDistributionDto> VegetationDistributions { get; set; } = new();
}

public class MapRegionObjectDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("objectType")]
    public MapObjectType ObjectType { get; set; }

    [JsonPropertyName("shape")]
    public MapObjectShape Shape { get; set; } = MapObjectShape.Rectangle;

    [JsonPropertyName("startX")]
    public int StartX { get; set; }

    [JsonPropertyName("startY")]
    public int StartY { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("strength")]
    public double Strength { get; set; } = 1.0;

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 0;
}

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
}

public class SimulationStatusDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Status { get; set; }
    public int CurrentStep { get; set; }
    public bool IsRunning { get; set; }
    public int TotalBurnedCells { get; set; }
    public int TotalBurningCells { get; set; }
    public double FireArea { get; set; }
    public double Precipitation { get; set; }
    public GraphType GraphType { get; set; } = GraphType.Grid;
    public string? Warning { get; set; }

    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double WindSpeed { get; set; }
    public string WindDirection { get; set; } = "—";
    public double WindDirectionDegrees { get; set; }

    public string StatusText => Status switch
    {
        0 => "Создана",
        1 => "Запущена",
        2 => "Завершена",
        3 => "Отменена",
        _ => "Неизвестно"
    };

    public string GraphTypeText => GraphType switch
    {
        GraphType.Grid => "Сетка",
        GraphType.ClusteredGraph => "Кластерный граф",
        GraphType.RegionClusterGraph => "Региональный кластерный граф",
        _ => "Неизвестно"
    };
}

public class StepResultDto
{
    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("burningCellsCount")]
    public int BurningCellsCount { get; set; }

    [JsonPropertyName("burnedCellsCount")]
    public int BurnedCellsCount { get; set; }

    [JsonPropertyName("newlyIgnitedCells")]
    public int NewlyIgnitedCells { get; set; }

    [JsonPropertyName("fireArea")]
    public double FireArea { get; set; }
}

public class VegetationDistributionDto
{
    [JsonPropertyName("vegetationType")]
    public int VegetationType { get; set; }

    [JsonPropertyName("probability")]
    public double Probability { get; set; }
}