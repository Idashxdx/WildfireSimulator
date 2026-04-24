using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WildfireSimulator.Application.Features.Simulations.DTOs;
using WildfireSimulator.Application.Interfaces;
using WildfireSimulator.Domain.Models;
using WildfireSimulator.Infrastructure.Data;

namespace WildfireSimulator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulationsController : ControllerBase
{
    private readonly ISimulationRepository _simulationRepository;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SimulationsController> _logger;
    private readonly IFireMetricsRepository _fireMetricsRepository;
    public SimulationsController(
     ISimulationRepository simulationRepository,
     IFireMetricsRepository fireMetricsRepository,
     ApplicationDbContext context,
     ILogger<SimulationsController> logger)
    {
        _simulationRepository = simulationRepository;
        _fireMetricsRepository = fireMetricsRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet("{id}/metrics")]
    public async Task<IActionResult> GetMetrics(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var simulation = await _simulationRepository.GetByIdAsync(id, cancellationToken);
            if (simulation == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Симуляция с ID {id} не найдена"
                });
            }

            var metrics = await _fireMetricsRepository.GetBySimulationIdAsync(id, cancellationToken);

            var result = metrics
                .Select(m => FireMetricsHistoryDto.FromEntity(m))
                .ToList();

            return Ok(new
            {
                success = true,
                simulationId = id,
                count = result.Count,
                metrics = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении метрик симуляции с ID {Id}", id);
            return StatusCode(500, new
            {
                success = false,
                message = "Внутренняя ошибка сервера"
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var simulations = await _simulationRepository.GetAllAsync(cancellationToken);
            var dtos = simulations.Select(SimulationDto.FromEntity);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении списка симуляций");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var simulation = await _simulationRepository.GetByIdAsync(id, cancellationToken);
            if (simulation == null)
                return NotFound($"Симуляция с ID {id} не найдена");

            return Ok(SimulationDto.FromEntity(simulation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении симуляции с ID {Id}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSimulationWithWeatherRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var effectiveScenarioType = request.GraphType == GraphType.Grid
                ? ResolveGridScenarioType(request.SelectedDemoPreset, request.ScenarioType)
                : null;

            GraphScaleType? effectiveGraphScaleType = request.GraphType == GraphType.Grid
     ? null
     : request.GraphScaleType ?? GraphScaleType.Medium;
            var effectiveMapCreationMode = request.GraphType switch
            {
                GraphType.Grid when request.PreparedMap != null => MapCreationMode.Random,
                GraphType.ClusteredGraph when effectiveGraphScaleType == GraphScaleType.Small => MapCreationMode.Random,
                _ => request.MapCreationMode
            };

            var effectiveClusteredScenarioType =
                request.GraphType == GraphType.ClusteredGraph &&
                effectiveGraphScaleType != GraphScaleType.Small &&
                effectiveMapCreationMode == MapCreationMode.Scenario
                    ? request.ClusteredScenarioType
                    : null;

            var effectiveClusteredBlueprint =
                request.GraphType == GraphType.ClusteredGraph &&
                effectiveMapCreationMode == MapCreationMode.SemiManual &&
                request.ClusteredBlueprint != null &&
                request.ClusteredBlueprint.Nodes.Any()
                    ? request.ClusteredBlueprint
                    : null;

            var parameters = new SimulationParameters
            {
                GridWidth = request.PreparedMap?.Width > 0 ? request.PreparedMap.Width : request.GridWidth,
                GridHeight = request.PreparedMap?.Height > 0 ? request.PreparedMap.Height : request.GridHeight,
                GraphType = request.GraphType,
                GraphScaleType = effectiveGraphScaleType,

                InitialMoistureMin = request.InitialMoistureMin,
                InitialMoistureMax = request.InitialMoistureMax,
                ElevationVariation = request.ElevationVariation,
                InitialFireCellsCount = request.InitialFireCellsCount,
                SimulationSteps = request.SimulationSteps,
                StepDurationSeconds = request.StepDurationSeconds,
                RandomSeed = request.RandomSeed,
                MapCreationMode = effectiveMapCreationMode,

                ScenarioType = effectiveScenarioType,
                ClusteredScenarioType = effectiveClusteredScenarioType,

                MapNoiseStrength = request.MapNoiseStrength,
                MapDrynessFactor = request.MapDrynessFactor,
                ReliefStrengthFactor = request.ReliefStrengthFactor,
                FuelDensityFactor = request.FuelDensityFactor
            };

            if (request.VegetationDistributions != null && request.VegetationDistributions.Any())
            {
                parameters.VegetationDistributions = request.VegetationDistributions
                    .Select(v => new VegetationDistribution
                    {
                        VegetationType = v.VegetationType,
                        Probability = v.Probability
                    })
                    .ToList();
            }

            if (request.GraphType == GraphType.Grid &&
                request.MapRegionObjects != null &&
                request.MapRegionObjects.Any())
            {
                parameters.MapRegionObjects = request.MapRegionObjects
                    .Select(x => new MapRegionObject
                    {
                        Id = x.Id == Guid.Empty ? Guid.NewGuid() : x.Id,
                        ObjectType = x.ObjectType,
                        Shape = MapObjectShape.Rectangle,
                        StartX = x.StartX,
                        StartY = x.StartY,
                        Width = x.Width,
                        Height = x.Height,
                        Strength = x.Strength,
                        Priority = x.Priority
                    })
                    .ToList();
            }

            if (effectiveClusteredBlueprint != null)
            {
                parameters.ClusteredBlueprint = new ClusteredGraphBlueprint
                {
                    CanvasWidth = effectiveClusteredBlueprint.CanvasWidth,
                    CanvasHeight = effectiveClusteredBlueprint.CanvasHeight,
                    Candidates = effectiveClusteredBlueprint.Candidates
                        .Select(x => new ClusteredCandidateNode
                        {
                            Id = x.Id == Guid.Empty ? Guid.NewGuid() : x.Id,
                            X = x.X,
                            Y = x.Y
                        })
                        .ToList(),
                    Nodes = effectiveClusteredBlueprint.Nodes
                        .Select(x => new ClusteredNodeDraft
                        {
                            Id = x.Id == Guid.Empty ? Guid.NewGuid() : x.Id,
                            X = x.X,
                            Y = x.Y,
                            ClusterId = x.ClusterId ?? string.Empty,
                            Vegetation = x.Vegetation,
                            Moisture = x.Moisture,
                            Elevation = x.Elevation
                        })
                        .ToList(),
                    Edges = effectiveClusteredBlueprint.Edges
                        .Select(x => new ClusteredEdgeDraft
                        {
                            Id = x.Id == Guid.Empty ? Guid.NewGuid() : x.Id,
                            FromNodeId = x.FromNodeId,
                            ToNodeId = x.ToNodeId,
                            DistanceOverride = x.DistanceOverride,
                            FireSpreadModifier = x.FireSpreadModifier
                        })
                        .ToList()
                };
            }

            var simulation = new Simulation(
                request.Name,
                request.Description,
                parameters);

            if (request.InitialFirePositions != null && request.InitialFirePositions.Any())
            {
                var fixedPositions = request.InitialFirePositions
                    .Select(p => (p.X, p.Y))
                    .ToList();

                simulation.SaveInitialFirePositions(fixedPositions);
            }

            var weather = new WeatherCondition(
                DateTime.UtcNow,
                request.Temperature,
                request.Humidity,
                request.WindSpeed,
                request.WindDirection,
                request.Precipitation);

            await _context.WeatherConditions.AddAsync(weather, cancellationToken);
            simulation.SetWeatherCondition(weather);

            if (request.GraphType == GraphType.Grid && request.PreparedMap != null)
            {
                var preparedGraph = BuildGridGraphFromPreparedMap(request.PreparedMap, parameters);
                simulation.SaveGraph(preparedGraph);
            }

            await _simulationRepository.AddAsync(simulation, cancellationToken);

            _logger.LogInformation(
                "Создана симуляция {Id}. GraphType={GraphType}, GraphScaleType={GraphScaleType}, Mode={Mode}, GridScenario={GridScenario}, GraphScenario={GraphScenario}, GridObjects={ObjectsCount}, GraphNodes={GraphNodesCount}, HasPreparedMap={HasPreparedMap}",
                simulation.Id,
                simulation.Parameters.GraphType,
                simulation.Parameters.GraphScaleType,
                simulation.Parameters.MapCreationMode,
                simulation.Parameters.ScenarioType,
                simulation.Parameters.ClusteredScenarioType,
                simulation.Parameters.MapRegionObjects.Count,
                simulation.Parameters.ClusteredBlueprint?.Nodes.Count ?? 0,
                request.PreparedMap != null);

            return CreatedAtAction(
                nameof(GetById),
                new { id = simulation.Id },
                SimulationDto.FromEntity(simulation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании симуляции");
            return StatusCode(500, new
            {
                success = false,
                message = "Внутренняя ошибка сервера",
                error = ex.Message
            });
        }
    }

    private static MapScenarioType? ResolveGridScenarioType(string? selectedDemoPreset, MapScenarioType? fallback)
    {
        if (string.IsNullOrWhiteSpace(selectedDemoPreset))
            return fallback;

        return selectedDemoPreset.Trim().ToLowerInvariant() switch
        {
            "dry-coniferous" => MapScenarioType.DryConiferousMassif,
            "river" => MapScenarioType.ForestWithRiver,
            "lake" => MapScenarioType.ForestWithLake,
            "firebreak" => MapScenarioType.ForestWithFirebreak,
            "hills" => MapScenarioType.HillyTerrain,
            "wet" => MapScenarioType.WetForestAfterRain,
            "mixed" => MapScenarioType.MixedForest,

            "dryconiferousmassif" => MapScenarioType.DryConiferousMassif,
            "forestwithriver" => MapScenarioType.ForestWithRiver,
            "forestwithlake" => MapScenarioType.ForestWithLake,
            "forestwithfirebreak" => MapScenarioType.ForestWithFirebreak,
            "hillyterrain" => MapScenarioType.HillyTerrain,
            "wetforestafterrain" => MapScenarioType.WetForestAfterRain,
            "mixedforest" => MapScenarioType.MixedForest,

            _ => fallback
        };
    }
    private ForestGraph BuildGridGraphFromPreparedMap(
        PreparedGridMapRequest preparedMap,
        SimulationParameters parameters)
    {
        var width = Math.Max(1, preparedMap.Width);
        var height = Math.Max(1, preparedMap.Height);

        var graph = new ForestGraph
        {
            Width = width,
            Height = height,
            StepDurationSeconds = parameters.StepDurationSeconds > 0
                ? parameters.StepDurationSeconds
                : 60
        };

        var groupedCells = (preparedMap.Cells ?? new List<PreparedGridCellRequest>())
            .Where(c => c != null)
            .GroupBy(c => (c.X, c.Y))
            .Select(g => g.Last())
            .Where(c => c.X >= 0 && c.Y >= 0 && c.X < width && c.Y < height)
            .ToDictionary(c => (c.X, c.Y), c => c);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!groupedCells.TryGetValue((x, y), out var preparedCell))
                {
                    preparedCell = new PreparedGridCellRequest
                    {
                        X = x,
                        Y = y,
                        Vegetation = VegetationType.Mixed.ToString(),
                        Moisture = Math.Clamp(
                            (parameters.InitialMoistureMin + parameters.InitialMoistureMax) / 2.0,
                            0.0,
                            1.0),
                        Elevation = 0.0
                    };
                }

                var vegetation = ParsePreparedVegetation(preparedCell.Vegetation);
                var moisture = Math.Clamp(preparedCell.Moisture, 0.0, 1.0);
                var elevation = preparedCell.Elevation;

                var cell = new ForestCell(
                    x,
                    y,
                    vegetation,
                    moisture,
                    elevation);

                graph.Cells.Add(cell);
            }
        }

        CreatePreparedGridEdges(graph, width, height);
        ApplyPreparedSurfaceBarrierEdgeModifiers(graph);

        return graph;
    }
    private static VegetationType ParsePreparedVegetation(string? vegetation)
    {
        if (string.IsNullOrWhiteSpace(vegetation))
            return VegetationType.Mixed;

        return vegetation.Trim().ToLowerInvariant() switch
        {
            "grass" => VegetationType.Grass,
            "shrub" => VegetationType.Shrub,
            "deciduous" => VegetationType.Deciduous,
            "coniferous" => VegetationType.Coniferous,
            "mixed" => VegetationType.Mixed,
            "water" => VegetationType.Water,
            "bare" => VegetationType.Bare,

            "трава" => VegetationType.Grass,
            "кустарник" => VegetationType.Shrub,
            "лиственный" => VegetationType.Deciduous,
            "лиственный лес" => VegetationType.Deciduous,
            "хвойный" => VegetationType.Coniferous,
            "хвойный лес" => VegetationType.Coniferous,
            "смешанный" => VegetationType.Mixed,
            "смешанный лес" => VegetationType.Mixed,
            "вода" => VegetationType.Water,
            "пустая поверхность" => VegetationType.Bare,
            "голая поверхность" => VegetationType.Bare,

            _ => Enum.TryParse<VegetationType>(vegetation, true, out var parsed)
                ? parsed
                : VegetationType.Mixed
        };
    }
    private static void CreatePreparedGridEdges(ForestGraph graph, int width, int height)
    {
        var cellMap = graph.Cells.ToDictionary(c => (c.X, c.Y), c => c);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!cellMap.TryGetValue((x, y), out var from))
                    continue;

                foreach (var (nx, ny) in GetPreparedGridNeighbors8(x, y, width, height))
                {
                    if (nx < x || (nx == x && ny <= y))
                        continue;

                    if (!cellMap.TryGetValue((nx, ny), out var to))
                        continue;

                    var distance = CalculatePreparedDistance(from.X, from.Y, to.X, to.Y);
                    var slope = distance <= 0.0001 ? 0.0 : (to.Elevation - from.Elevation) / distance;

                    graph.Edges.Add(new ForestEdge(from, to, distance, slope));
                }
            }
        }
    }
    private static IEnumerable<(int X, int Y)> GetPreparedGridNeighbors8(int x, int y, int width, int height)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                int nx = x + dx;
                int ny = y + dy;

                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    continue;

                yield return (nx, ny);
            }
        }
    }
    private static double CalculatePreparedDistance(int x1, int y1, int x2, int y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }
    private static void ApplyPreparedSurfaceBarrierEdgeModifiers(ForestGraph graph)
    {
        foreach (var edge in graph.Edges)
        {
            var from = edge.FromCell;
            var to = edge.ToCell;

            if (from == null || to == null)
                continue;

            double factor = 1.0;

            bool fromBarrier = from.Vegetation == VegetationType.Water || from.Vegetation == VegetationType.Bare;
            bool toBarrier = to.Vegetation == VegetationType.Water || to.Vegetation == VegetationType.Bare;

            if (fromBarrier || toBarrier)
                factor *= 0.25;

            if (from.Vegetation == VegetationType.Water || to.Vegetation == VegetationType.Water)
                factor *= 0.35;

            if (from.Vegetation == VegetationType.Bare || to.Vegetation == VegetationType.Bare)
                factor *= 0.55;

            SetPreparedEdgeFireSpreadModifier(edge, Math.Clamp(edge.FireSpreadModifier * factor, 0.02, 1.15));
        }
    }
    private static void SetPreparedEdgeFireSpreadModifier(ForestEdge edge, double value)
    {
        var property = typeof(ForestEdge).GetProperty(nameof(ForestEdge.FireSpreadModifier));
        property?.SetValue(edge, value);
    }

    [HttpGet("status/{status}")]
    public async Task<IActionResult> GetByStatus(SimulationStatus status, CancellationToken cancellationToken)
    {
        try
        {
            var simulations = await _simulationRepository.GetByStatusAsync(status, cancellationToken);
            var dtos = simulations.Select(SimulationDto.FromEntity);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении симуляций по статусу {Status}", status);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    [HttpPost("{id}/finish")]
    public async Task<IActionResult> FinishSimulation(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var simulation = await _simulationRepository.GetByIdAsync(id, cancellationToken);
            if (simulation == null)
                return NotFound($"Симуляция с ID {id} не найдена");

            if (simulation.Status != SimulationStatus.Running)
                return BadRequest("Симуляция не запущена");

            simulation.Finish();
            await _simulationRepository.UpdateAsync(simulation, cancellationToken);

            return Ok(SimulationDto.FromEntity(simulation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при завершении симуляции с ID {Id}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    [HttpGet("{id}/weather")]
    public async Task<IActionResult> GetSimulationWeather(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var simulation = await _simulationRepository.GetByIdAsync(id, cancellationToken);
            if (simulation == null)
                return NotFound($"Симуляция с ID {id} не найдена");

            var weather = await _context.WeatherConditions
                .FirstOrDefaultAsync(w => w.Id == simulation.WeatherConditionId, cancellationToken);

            if (weather == null)
                return NotFound($"Погодные условия для симуляции {id} не найдены");

            return Ok(new
            {
                temperature = weather.Temperature,
                humidity = weather.Humidity,
                windSpeed = weather.WindSpeedMps,
                windDirectionDegrees = weather.WindDirectionDegrees,
                windDirection = weather.WindDirection.ToString(),
                precipitation = weather.Precipitation,
                timestamp = weather.Timestamp
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении погоды для симуляции с ID {Id}", id);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var simulation = await _context.Simulations
                .Include(s => s.Metrics)
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (simulation == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Симуляция с ID {id} не найдена"
                });
            }

            var activeRecord = await _context.ActiveSimulationRecords
                .FirstOrDefaultAsync(x => x.SimulationId == id, cancellationToken);

            if (activeRecord != null)
                _context.ActiveSimulationRecords.Remove(activeRecord);

            var metrics = await _context.FireMetrics
                .Where(x => x.SimulationId == id)
                .ToListAsync(cancellationToken);

            if (metrics.Count > 0)
                _context.FireMetrics.RemoveRange(metrics);

            if (simulation.WeatherConditionId.HasValue)
            {
                var weather = await _context.WeatherConditions
                    .FirstOrDefaultAsync(w => w.Id == simulation.WeatherConditionId.Value, cancellationToken);

                _context.Simulations.Remove(simulation);
                await _context.SaveChangesAsync(cancellationToken);

                if (weather != null)
                {
                    var weatherStillUsed = await _context.Simulations
                        .AnyAsync(s => s.WeatherConditionId == weather.Id, cancellationToken);

                    if (!weatherStillUsed)
                    {
                        _context.WeatherConditions.Remove(weather);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            else
            {
                _context.Simulations.Remove(simulation);
                await _context.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Симуляция {Id} удалена", id);

            return Ok(new
            {
                success = true,
                message = $"Симуляция {id} удалена"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении симуляции с ID {Id}", id);
            return StatusCode(500, new
            {
                success = false,
                message = "Внутренняя ошибка сервера",
                error = ex.Message
            });
        }
    }
}
public class CreateSimulationWithWeatherRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int GridWidth { get; set; } = 20;
    public int GridHeight { get; set; } = 20;
    public GraphType GraphType { get; set; } = GraphType.Grid;

    public GraphScaleType? GraphScaleType { get; set; }

    public double InitialMoistureMin { get; set; } = 0.3;
    public double InitialMoistureMax { get; set; } = 0.7;
    public double ElevationVariation { get; set; } = 50.0;
    public int InitialFireCellsCount { get; set; } = 3;
    public int SimulationSteps { get; set; } = 100;
    public int StepDurationSeconds { get; set; } = 60;
    public int? RandomSeed { get; set; }

    public MapCreationMode MapCreationMode { get; set; } = MapCreationMode.Random;

    public MapScenarioType? ScenarioType { get; set; }

    public ClusteredScenarioType? ClusteredScenarioType { get; set; }

    public ClusteredBlueprintRequest? ClusteredBlueprint { get; set; }

    public double MapNoiseStrength { get; set; } = 0.08;
    public double MapDrynessFactor { get; set; } = 1.0;
    public double ReliefStrengthFactor { get; set; } = 1.0;
    public double FuelDensityFactor { get; set; } = 1.0;

    public List<MapRegionObjectRequest> MapRegionObjects { get; set; } = new();

    public List<VegetationDistributionRequest> VegetationDistributions { get; set; } = new();

    public List<InitialFirePositionDto> InitialFirePositions { get; set; } = new();

    public string? SelectedDemoPreset { get; set; }

    public PreparedGridMapRequest? PreparedMap { get; set; }

    public double Temperature { get; set; } = 25.0;
    public double Humidity { get; set; } = 40.0;
    public double WindSpeed { get; set; } = 5.0;
    public double WindDirection { get; set; } = 45.0;
    public double Precipitation { get; set; } = 0.0;
}
public class PreparedGridMapRequest
{
    public int Width { get; set; }
    public int Height { get; set; }
    public List<PreparedGridCellRequest> Cells { get; set; } = new();
}
public class VegetationDistributionRequest
{
    public VegetationType VegetationType { get; set; }
    public double Probability { get; set; }
}
public class PreparedGridCellRequest
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Vegetation { get; set; } = string.Empty;
    public double Moisture { get; set; }
    public double Elevation { get; set; }
}
public class InitialFirePositionDto
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class MapRegionObjectRequest
{
    public Guid Id { get; set; }
    public MapObjectType ObjectType { get; set; }
    public MapObjectShape Shape { get; set; } = MapObjectShape.Rectangle;
    public int StartX { get; set; }
    public int StartY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Strength { get; set; } = 1.0;
    public int Priority { get; set; } = 0;
}

public class ClusteredBlueprintRequest
{
    public int CanvasWidth { get; set; } = 24;
    public int CanvasHeight { get; set; } = 24;

    public List<ClusteredCandidateNodeRequest> Candidates { get; set; } = new();
    public List<ClusteredNodeDraftRequest> Nodes { get; set; } = new();
    public List<ClusteredEdgeDraftRequest> Edges { get; set; } = new();
}

public class ClusteredCandidateNodeRequest
{
    public Guid Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public class ClusteredNodeDraftRequest
{
    public Guid Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string? ClusterId { get; set; }
    public VegetationType Vegetation { get; set; } = VegetationType.Mixed;
    public double Moisture { get; set; } = 0.45;
    public double Elevation { get; set; } = 0.0;
}

public class ClusteredEdgeDraftRequest
{
    public Guid Id { get; set; }
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public double? DistanceOverride { get; set; }
    public double FireSpreadModifier { get; set; } = 1.0;
}
