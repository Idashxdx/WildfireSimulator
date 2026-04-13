using System.Text.Json;
using Ardalis.GuardClauses;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace WildfireSimulator.Domain.Models;

public class Simulation
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public SimulationStatus Status { get; private set; }
    public SimulationParameters Parameters { get; private set; } = null!;
    public Guid? WeatherConditionId { get; private set; }

    public string? SerializedGraph { get; private set; }
    public string? InitialFirePositions { get; private set; }

    public virtual WeatherCondition? WeatherCondition { get; private set; }
    public virtual ICollection<FireMetrics> Metrics { get; private set; } = new List<FireMetrics>();

    [NotMapped]
    public ForestGraph? CachedGraph { get; set; }

    private Simulation() { }


    public void ClearInitialFirePositions()
    {
        InitialFirePositions = null;
    }
    public void SetWeatherCondition(WeatherCondition weatherCondition)
    {
        WeatherCondition = Guard.Against.Null(weatherCondition, nameof(weatherCondition));
        WeatherConditionId = weatherCondition.Id;
    }

    public Simulation(string name, string description, SimulationParameters parameters)
    {
        Id = Guid.NewGuid();
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        Description = description ?? string.Empty;
        CreatedAt = DateTime.UtcNow;
        Status = SimulationStatus.Created;
        Parameters = Guard.Against.Null(parameters, nameof(parameters));
    }

    public void Start(WeatherCondition weatherCondition)
    {
        if (Status == SimulationStatus.Created)
        {
            Status = SimulationStatus.Running;
            StartedAt = DateTime.UtcNow;
            WeatherCondition = Guard.Against.Null(weatherCondition, nameof(weatherCondition));
            WeatherConditionId = weatherCondition.Id;
        }
    }

    public void Finish()
    {
        if (Status == SimulationStatus.Running)
        {
            Status = SimulationStatus.Completed;
            FinishedAt = DateTime.UtcNow;
        }
    }

    public void Cancel()
    {
        if (Status == SimulationStatus.Running || Status == SimulationStatus.Created)
        {
            Status = SimulationStatus.Cancelled;
            FinishedAt = DateTime.UtcNow;
        }
    }

    public void UpdateParameters(SimulationParameters newParameters)
    {
        if (Status == SimulationStatus.Created)
        {
            Parameters = Guard.Against.Null(newParameters, nameof(newParameters));
        }
    }

    public void SaveGraph(ForestGraph graph)
    {
        graph = Guard.Against.Null(graph, nameof(graph));

        var dto = new GraphSerializationDto
        {
            Width = graph.Width,
            Height = graph.Height,
            StepDurationSeconds = graph.StepDurationSeconds > 0
                ? graph.StepDurationSeconds
                : Parameters.StepDurationSeconds,
            Cells = graph.Cells.Select(c => new CellSerializationDto
            {
                Id = c.Id,
                X = c.X,
                Y = c.Y,
                ClusterId = c.ClusterId,
                Vegetation = c.Vegetation,
                Moisture = c.Moisture,
                Elevation = c.Elevation,
                State = c.State,
                BurnProbability = c.BurnProbability,
                IgnitionTime = c.IgnitionTime,
                BurnoutTime = c.BurnoutTime,
                FuelLoad = c.FuelLoad,
                CurrentFuelLoad = c.CurrentFuelLoad,
                BurnRate = c.BurnRate,
                FireStage = c.FireStage,
                FireIntensity = c.FireIntensity,
                BurningElapsedSeconds = c.BurningElapsedSeconds,
                AccumulatedHeatJ = c.AccumulatedHeatJ
            }).ToList(),
            Edges = graph.Edges.Select(e => new EdgeSerializationDto
            {
                Id = e.Id,
                FromCellId = e.FromCellId,
                ToCellId = e.ToCellId,
                Distance = e.Distance,
                Slope = e.Slope,
                FireSpreadModifier = e.FireSpreadModifier
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        SerializedGraph = JsonSerializer.Serialize(dto, options);
        CachedGraph = graph;
    }

    public ForestGraph? LoadGraph()
    {
        if (CachedGraph != null)
            return CachedGraph;

        if (string.IsNullOrWhiteSpace(SerializedGraph))
            return null;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        var dto = JsonSerializer.Deserialize<GraphSerializationDto>(SerializedGraph, options);
        if (dto == null)
            return null;

        var graph = new ForestGraph
        {
            Width = dto.Width,
            Height = dto.Height,
            StepDurationSeconds = dto.StepDurationSeconds > 0
                ? dto.StepDurationSeconds
                : Parameters.StepDurationSeconds
        };

        var cellDict = new Dictionary<Guid, ForestCell>();

        foreach (var cellDto in dto.Cells)
        {
            var cell = new ForestCell(
                cellDto.X,
                cellDto.Y,
                cellDto.Vegetation,
                cellDto.Moisture,
                cellDto.Elevation,
                cellDto.ClusterId);

            SetBackingField(cell, "<Id>k__BackingField", cellDto.Id);
            SetBackingField(cell, "<State>k__BackingField", cellDto.State);
            SetBackingField(cell, "<IgnitionTime>k__BackingField", cellDto.IgnitionTime);
            SetBackingField(cell, "<BurnoutTime>k__BackingField", cellDto.BurnoutTime);
            SetBackingField(cell, "<FuelLoad>k__BackingField", cellDto.FuelLoad);
            SetBackingField(cell, "<CurrentFuelLoad>k__BackingField", cellDto.CurrentFuelLoad);
            SetBackingField(cell, "<BurnRate>k__BackingField", cellDto.BurnRate);
            SetBackingField(cell, "<FireStage>k__BackingField", cellDto.FireStage);
            SetBackingField(cell, "<FireIntensity>k__BackingField", cellDto.FireIntensity);
            SetBackingField(cell, "<BurningElapsedSeconds>k__BackingField", cellDto.BurningElapsedSeconds);
            SetBackingField(cell, "<AccumulatedHeatJ>k__BackingField", cellDto.AccumulatedHeatJ);

            cell.SetBurnProbability(cellDto.BurnProbability);

            graph.Cells.Add(cell);
            cellDict[cell.Id] = cell;
        }

        foreach (var edgeDto in dto.Edges)
        {
            if (!cellDict.TryGetValue(edgeDto.FromCellId, out var fromCell) ||
                !cellDict.TryGetValue(edgeDto.ToCellId, out var toCell))
            {
                continue;
            }

            var edge = new ForestEdge(fromCell, toCell, edgeDto.Distance, edgeDto.Slope);

            SetBackingField(edge, "<Id>k__BackingField", edgeDto.Id);
            SetBackingField(edge, "<FireSpreadModifier>k__BackingField", edgeDto.FireSpreadModifier);

            graph.Edges.Add(edge);
        }

        CachedGraph = graph;
        return graph;
    }

    public bool HasSavedGraph() => !string.IsNullOrWhiteSpace(SerializedGraph);

    public void SaveInitialFirePositions(List<(int X, int Y)> positions)
    {
        positions = positions ?? new List<(int X, int Y)>();

        var payload = positions
            .Select(p => new CoordinateDto { X = p.X, Y = p.Y })
            .ToList();

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        InitialFirePositions = JsonSerializer.Serialize(payload, options);
    }

    public List<(int X, int Y)> LoadInitialFirePositions()
    {
        if (string.IsNullOrWhiteSpace(InitialFirePositions))
            return new List<(int X, int Y)>();

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var typed = JsonSerializer.Deserialize<List<CoordinateDto>>(InitialFirePositions, options);
            if (typed != null && typed.Count > 0)
            {
                return typed
                    .Select(p => (p.X, p.Y))
                    .ToList();
            }
        }
        catch
        {
        }

        try
        {
            var dictionaries = JsonSerializer.Deserialize<List<Dictionary<string, int>>>(InitialFirePositions);
            if (dictionaries == null)
                return new List<(int X, int Y)>();

            var result = new List<(int X, int Y)>();

            foreach (var item in dictionaries)
            {
                if (TryGetCoordinate(item, "x", out var x) &&
                    TryGetCoordinate(item, "y", out var y))
                {
                    result.Add((x, y));
                }
            }

            return result;
        }
        catch
        {
            return new List<(int X, int Y)>();
        }
    }

    public void ResetForRestart()
    {
        Status = SimulationStatus.Created;
        StartedAt = null;
        FinishedAt = null;

        CachedGraph = null;
    }

    private static bool TryGetCoordinate(
        Dictionary<string, int> source,
        string key,
        out int value)
    {
        if (source.TryGetValue(key, out value))
            return true;

        if (source.TryGetValue(key.ToUpperInvariant(), out value))
            return true;

        var titleCase = char.ToUpperInvariant(key[0]) + key[1..];
        if (source.TryGetValue(titleCase, out value))
            return true;

        value = 0;
        return false;
    }

    private static void SetBackingField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (field != null)
            field.SetValue(target, value);
    }

    private sealed class CoordinateDto
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}

public enum SimulationStatus
{
    Created = 0,
    Running = 1,
    Completed = 2,
    Cancelled = 3
}

public class SimulationParameters
{
    public int GridWidth { get; set; }
    public int GridHeight { get; set; }
    public GraphType GraphType { get; set; }

    [NotMapped]
    public List<VegetationDistribution> VegetationDistributions { get; set; } = new();

    public string VegetationDistributionsJson
    {
        get => JsonSerializer.Serialize(VegetationDistributions);
        set => VegetationDistributions = JsonSerializer.Deserialize<List<VegetationDistribution>>(value) ?? new();
    }

    public double InitialMoistureMin { get; set; }
    public double InitialMoistureMax { get; set; }
    public double ElevationVariation { get; set; }
    public int InitialFireCellsCount { get; set; }
    public int SimulationSteps { get; set; }
    public int StepDurationSeconds { get; set; }
    public int? RandomSeed { get; set; }

    public SimulationParameters()
    {
        GridWidth = 20;
        GridHeight = 20;
        GraphType = GraphType.Grid;
        InitialMoistureMin = 0.3;
        InitialMoistureMax = 0.7;
        ElevationVariation = 50.0;
        InitialFireCellsCount = 3;
        SimulationSteps = 100;
        StepDurationSeconds = 60;
        RandomSeed = null;
    }
}

public enum GraphType
{
    Grid = 0,
    ClusteredGraph = 1,
    RegionClusterGraph = 2
}

public class VegetationDistribution
{
    public VegetationType VegetationType { get; set; }
    public double Probability { get; set; }
}