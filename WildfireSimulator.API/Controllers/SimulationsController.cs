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

            var parameters = new SimulationParameters
            {
                GridWidth = request.GridWidth,
                GridHeight = request.GridHeight,
                GraphType = request.GraphType,
                InitialMoistureMin = request.InitialMoistureMin,
                InitialMoistureMax = request.InitialMoistureMax,
                ElevationVariation = request.ElevationVariation,
                InitialFireCellsCount = request.InitialFireCellsCount,
                SimulationSteps = request.SimulationSteps,
                StepDurationSeconds = request.StepDurationSeconds,
                RandomSeed = request.RandomSeed,
                MapCreationMode = request.MapCreationMode,

                ScenarioType = request.GraphType == GraphType.Grid
                    ? request.ScenarioType
                    : null,

                ClusteredScenarioType = request.GraphType == GraphType.ClusteredGraph
                    ? request.ClusteredScenarioType
                    : null,

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
                        Shape = x.Shape,
                        StartX = x.StartX,
                        StartY = x.StartY,
                        Width = x.Width,
                        Height = x.Height,
                        Strength = x.Strength,
                        Priority = x.Priority
                    })
                    .ToList();
            }

            if (request.GraphType == GraphType.ClusteredGraph &&
                request.ClusteredBlueprint != null &&
                request.ClusteredBlueprint.Nodes.Any())
            {
                parameters.ClusteredBlueprint = new ClusteredGraphBlueprint
                {
                    CanvasWidth = request.ClusteredBlueprint.CanvasWidth,
                    CanvasHeight = request.ClusteredBlueprint.CanvasHeight,
                    Candidates = request.ClusteredBlueprint.Candidates
                        .Select(x => new ClusteredCandidateNode
                        {
                            Id = x.Id == Guid.Empty ? Guid.NewGuid() : x.Id,
                            X = x.X,
                            Y = x.Y
                        })
                        .ToList(),
                    Nodes = request.ClusteredBlueprint.Nodes
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
                    Edges = request.ClusteredBlueprint.Edges
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

            var simulation = new Simulation(request.Name, request.Description, parameters);

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
                request.Precipitation
            );

            await _context.WeatherConditions.AddAsync(weather, cancellationToken);
            simulation.SetWeatherCondition(weather);

            await _simulationRepository.AddAsync(simulation, cancellationToken);

            _logger.LogInformation(
                "Создана симуляция {Id}. GraphType={GraphType}, Mode={Mode}, GridScenario={GridScenario}, GraphScenario={GraphScenario}, GridObjects={ObjectsCount}, GraphNodes={GraphNodesCount}",
                simulation.Id,
                simulation.Parameters.GraphType,
                simulation.Parameters.MapCreationMode,
                simulation.Parameters.ScenarioType,
                simulation.Parameters.ClusteredScenarioType,
                simulation.Parameters.MapRegionObjects.Count,
                simulation.Parameters.ClusteredBlueprint?.Nodes.Count ?? 0);

            return CreatedAtAction(
                nameof(GetById),
                new { id = simulation.Id },
                SimulationDto.FromEntity(simulation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании симуляции");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
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
}
public class CreateSimulationWithWeatherRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int GridWidth { get; set; } = 20;
    public int GridHeight { get; set; } = 20;
    public GraphType GraphType { get; set; } = GraphType.Grid;
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

    public List<InitialFirePositionDto> InitialFirePositions { get; set; } = new();

    public double Temperature { get; set; } = 25.0;
    public double Humidity { get; set; } = 40.0;
    public double WindSpeed { get; set; } = 5.0;
    public double WindDirection { get; set; } = 45.0;
    public double Precipitation { get; set; } = 0.0;

    public List<VegetationDistributionRequest> VegetationDistributions { get; set; } = new();
}

public class InitialFirePositionDto
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class VegetationDistributionRequest
{
    public VegetationType VegetationType { get; set; }
    public double Probability { get; set; }
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
