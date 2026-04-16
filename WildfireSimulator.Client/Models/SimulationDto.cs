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
    Grass = 0,
    Shrub = 1,
    Deciduous = 2,
    Coniferous = 3,
    Mixed = 4,
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

public enum ClusteredScenarioType
{
    DenseDryConiferous = 0,
    WaterBarrier = 1,
    FirebreakGap = 2,
    HillyClusters = 3,
    WetAfterRain = 4,
    MixedDryHotspots = 5
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
    public double ElevationVariation { get; set; } = 50.0;

    [JsonPropertyName("initialFireCellsCount")]
    public int InitialFireCellsCount { get; set; } = 3;

    [JsonPropertyName("simulationSteps")]
    public int SimulationSteps { get; set; } = 100;

    [JsonPropertyName("stepDurationSeconds")]
    public int StepDurationSeconds { get; set; } = 60;

    [JsonPropertyName("randomSeed")]
    public int? RandomSeed { get; set; }

    [JsonPropertyName("mapCreationMode")]
    public MapCreationMode MapCreationMode { get; set; } = MapCreationMode.Random;

    // Только для Grid
    [JsonPropertyName("scenarioType")]
    public MapScenarioType? ScenarioType { get; set; }

    // Только для ClusteredGraph
    [JsonPropertyName("clusteredScenarioType")]
    public ClusteredScenarioType? ClusteredScenarioType { get; set; }

    [JsonPropertyName("mapNoiseStrength")]
    public double MapNoiseStrength { get; set; } = 0.08;

    [JsonPropertyName("mapDrynessFactor")]
    public double MapDrynessFactor { get; set; } = 1.0;

    [JsonPropertyName("reliefStrengthFactor")]
    public double ReliefStrengthFactor { get; set; } = 1.0;

    [JsonPropertyName("fuelDensityFactor")]
    public double FuelDensityFactor { get; set; } = 1.0;

    // Только для Grid SemiManual
    [JsonPropertyName("mapRegionObjects")]
    public List<MapRegionObjectDto> MapRegionObjects { get; set; } = new();

    // Только для ClusteredGraph SemiManual
    [JsonPropertyName("clusteredBlueprint")]
    public ClusteredGraphBlueprintDto? ClusteredBlueprint { get; set; }

    [JsonPropertyName("vegetationDistributions")]
    public List<VegetationDistributionDto> VegetationDistributions { get; set; } = new();

    [JsonPropertyName("initialFirePositions")]
    public List<InitialFirePositionDto> InitialFirePositions { get; set; } = new();

    [JsonPropertyName("precipitation")]
    public double Precipitation { get; set; } = 0.0;
}

public class VegetationDistributionDto
{
    [JsonPropertyName("vegetationType")]
    public VegetationType VegetationType { get; set; }

    [JsonPropertyName("probability")]
    public double Probability { get; set; }
}

public class InitialFirePositionDto
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}

public class MapRegionObjectDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

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

public class ClusteredGraphBlueprintDto
{
    [JsonPropertyName("canvasWidth")]
    public int CanvasWidth { get; set; } = 24;

    [JsonPropertyName("canvasHeight")]
    public int CanvasHeight { get; set; } = 24;

    [JsonPropertyName("candidates")]
    public List<ClusteredCandidateNodeDto> Candidates { get; set; } = new();

    [JsonPropertyName("nodes")]
    public List<ClusteredNodeDraftDto> Nodes { get; set; } = new();

    [JsonPropertyName("edges")]
    public List<ClusteredEdgeDraftDto> Edges { get; set; } = new();
}

public class ClusteredCandidateNodeDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}

public class ClusteredNodeDraftDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("clusterId")]
    public string ClusterId { get; set; } = string.Empty;

    [JsonPropertyName("vegetation")]
    public VegetationType Vegetation { get; set; } = VegetationType.Mixed;

    [JsonPropertyName("moisture")]
    public double Moisture { get; set; } = 0.45;

    [JsonPropertyName("elevation")]
    public double Elevation { get; set; } = 0.0;
}

public class ClusteredEdgeDraftDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("fromNodeId")]
    public Guid FromNodeId { get; set; }

    [JsonPropertyName("toNodeId")]
    public Guid ToNodeId { get; set; }

    [JsonPropertyName("distanceOverride")]
    public double? DistanceOverride { get; set; }

    [JsonPropertyName("fireSpreadModifier")]
    public double FireSpreadModifier { get; set; } = 1.0;
}