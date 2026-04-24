using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WildfireSimulator.Application.Features.Simulations.DTOs;
using WildfireSimulator.Application.Services;
using WildfireSimulator.Domain.Models;
using WildfireSimulator.Infrastructure.Data;
using WildfireSimulator.Application.Interfaces;

namespace WildfireSimulator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulationManagerController : ControllerBase
{
    private readonly SimulationManager _simulationManager;
    private readonly ApplicationDbContext _context;
    private readonly IForestGraphGenerator _graphGenerator;
    private readonly ILogger<SimulationManagerController> _logger;

    public SimulationManagerController(
        SimulationManager simulationManager,
        ApplicationDbContext context,
        IForestGraphGenerator graphGenerator,
        ILogger<SimulationManagerController> logger)
    {
        _simulationManager = simulationManager;
        _context = context;
        _graphGenerator = graphGenerator;
        _logger = logger;
    }

    [HttpPost("{simulationId}/start")]
    public async Task<IActionResult> StartSimulation(
     Guid simulationId,
     [FromBody] StartSimulationRequest? request,
     CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Запрос на запуск симуляции {SimulationId}", simulationId);

            var simulation = await _context.Simulations
                .Include(s => s.Parameters)
                .Include(s => s.WeatherCondition)
                .FirstOrDefaultAsync(s => s.Id == simulationId, cancellationToken);

            if (simulation == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Симуляция {simulationId} не найдена"
                });
            }

            var activeSimulation = await _simulationManager.GetSimulation(simulationId);

            if (activeSimulation != null && activeSimulation.IsRunning)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Симуляция уже запущена"
                });
            }

            if ((simulation.Status == SimulationStatus.Completed || simulation.Status == SimulationStatus.Cancelled) &&
                simulation.HasSavedGraph())
            {
                _logger.LogInformation(
                    "📦 Симуляция {SimulationId} завершена, но имеет сохранённый исходный граф. Выполняем replay от сохранённого состояния.",
                    simulationId);

                simulation.ResetForRestart();
                simulation.CachedGraph = null;
                await _context.SaveChangesAsync(cancellationToken);
            }
            else if (simulation.Status == SimulationStatus.Created && simulation.HasSavedGraph())
            {
                simulation.CachedGraph = null;
                _logger.LogInformation("📦 Симуляция {SimulationId} имеет сохранённый граф, используем его.", simulationId);
            }
            else if (simulation.Status == SimulationStatus.Running && simulation.HasSavedGraph())
            {
                _logger.LogWarning(
                    "⚠️ Симуляция {SimulationId} имеет статус Running, но не активна в памяти. Считаем её прерванной и выполняем восстановимый replay.",
                    simulationId);

                simulation.Cancel();
                simulation.ResetForRestart();
                simulation.CachedGraph = null;
                await _context.SaveChangesAsync(cancellationToken);
            }

            WeatherCondition weather;

            if (simulation.WeatherCondition != null)
            {
                weather = simulation.WeatherCondition;
                _logger.LogInformation(
                    "Используем сохранённую погоду: {Temp}°C, ветер {WindSpeed} м/с, направление {WindDir}°",
                    weather.Temperature, weather.WindSpeedMps, weather.WindDirectionDegrees);
            }
            else
            {
                weather = new WeatherCondition(
                    DateTime.UtcNow,
                    25.0,
                    40.0,
                    5.0,
                    45.0,
                    0.0
                );

                await _context.WeatherConditions.AddAsync(weather, cancellationToken);
                simulation.SetWeatherCondition(weather);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Создана погода по умолчанию");
            }

            List<(int X, int Y)>? manualIgnitionPositions = null;

            if (request?.IgnitionMode?.Equals("manual", StringComparison.OrdinalIgnoreCase) == true)
            {
                manualIgnitionPositions = request.InitialFirePositions?
                    .Select(p => (p.X, p.Y))
                    .Distinct()
                    .ToList();

                if (manualIgnitionPositions == null || manualIgnitionPositions.Count == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Для ручного режима запуска нужно выбрать хотя бы одну клетку."
                    });
                }
            }

            var startedSimulation = await _simulationManager.StartSimulationAsync(
                simulation,
                weather,
                manualIgnitionPositions);

            var cells = startedSimulation.Graph.Cells
                .Select(BuildRichCellDto)
                .ToList();

            return Ok(new
            {
                success = true,
                message = $"Симуляция {simulation.Name} успешно запущена",
                simulationId = simulation.Id,
                ignitionMode = request?.IgnitionMode ?? "saved-or-random",
                graphType = simulation.Parameters.GraphType,
                activeSimulation = BuildActiveSimulationStartDto(startedSimulation, weather),
                cells = cells
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Ошибка при запуске симуляции {SimulationId}", simulationId);
            return BadRequest(new
            {
                success = false,
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при запуске симуляции {SimulationId}", simulationId);
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка при запуске симуляции",
                error = ex.Message
            });
        }
    }


    [HttpPost("{simulationId}/step")]
    public async Task<IActionResult> ExecuteStep(Guid simulationId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Запрос на выполнение шага симуляции {SimulationId}", simulationId);

            var stepResult = await _simulationManager.ExecuteStepAsync(simulationId);

            var activeSimulation = await _simulationManager.GetSimulation(simulationId);
            var cells = activeSimulation?.Graph.Cells
                .Select(BuildStepCellDto)
                .ToList() ?? new List<object>();

            return Ok(new
            {
                success = true,
                message = $"Шаг {stepResult.Step} выполнен",
                step = new
                {
                    step = stepResult.Step,
                    simulationId = stepResult.SimulationId,
                    timestamp = stepResult.Timestamp,
                    burningCellsCount = stepResult.BurningCellsCount,
                    burnedCellsCount = stepResult.BurnedCellsCount,
                    newlyIgnitedCells = stepResult.NewlyIgnitedCells,
                    totalCellsAffected = stepResult.TotalCellsAffected,
                    fireArea = stepResult.FireArea,
                    spreadSpeed = stepResult.SpreadSpeed
                },
                cells = cells,
                isRunning = activeSimulation?.IsRunning ?? false,
                status = activeSimulation != null ? (int)activeSimulation.Simulation.Status : (int?)null
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Ошибка выполнения шага для симуляции {SimulationId}", simulationId);
            return BadRequest(new
            {
                success = false,
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выполнении шага симуляции {SimulationId}", simulationId);
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка при выполнении шага симуляции",
                error = ex.Message
            });
        }
    }

    [HttpPost("{simulationId}/stop")]
    public async Task<IActionResult> StopSimulation(Guid simulationId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Запрос на остановку симуляции {SimulationId}", simulationId);

            var stopped = await _simulationManager.StopSimulation(simulationId);

            if (!stopped)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Симуляция {simulationId} не найдена"
                });
            }

            return Ok(new
            {
                success = true,
                message = $"Симуляция {simulationId} остановлена"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при остановке симуляции {SimulationId}", simulationId);
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка при остановке симуляции",
                error = ex.Message
            });
        }
    }

    [HttpGet("{simulationId}/status")]
    public async Task<IActionResult> GetSimulationStatus(Guid simulationId)
    {
        try
        {
            var activeSimulation = await _simulationManager.GetSimulation(simulationId);

            if (activeSimulation != null)
            {
                return Ok(new
                {
                    success = true,
                    simulation = new
                    {
                        id = activeSimulation.Simulation.Id,
                        name = activeSimulation.Simulation.Name,
                        status = (int)activeSimulation.Simulation.Status,
                        currentStep = activeSimulation.CurrentStep,
                        isRunning = activeSimulation.IsRunning,
                        startTime = activeSimulation.StartTime,
                        endTime = activeSimulation.EndTime,
                        duration = activeSimulation.Duration.TotalSeconds,
                        totalBurnedCells = activeSimulation.TotalBurnedCells,
                        totalBurningCells = activeSimulation.TotalBurningCells,
                        fireArea = activeSimulation.FireArea,
                        averageSpreadSpeed = activeSimulation.AverageSpreadSpeed,
                        graphType = activeSimulation.Simulation.Parameters.GraphType,
                        precipitation = activeSimulation.CurrentWeather.Precipitation
                    },
                    weather = BuildWeatherDto(activeSimulation.CurrentWeather),
                    graph = new
                    {
                        width = activeSimulation.Graph.Width,
                        height = activeSimulation.Graph.Height,
                        totalCells = activeSimulation.Graph.Cells.Count,
                        totalEdges = activeSimulation.Graph.Edges.Count
                    }
                });
            }

            var simulation = await _context.Simulations
                .Include(s => s.WeatherCondition)
                .FirstOrDefaultAsync(s => s.Id == simulationId);

            if (simulation == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Симуляция {simulationId} не найдена"
                });
            }

            _logger.LogInformation("📋 Статус симуляции из БД: {Name}, Status={Status}",
                simulation.Name, simulation.Status);

            simulation.CachedGraph = null;
            var graph2 = simulation.LoadGraph();

            var isInterruptedRunning = simulation.Status == SimulationStatus.Running;
            var precipitation = simulation.WeatherCondition?.Precipitation ?? 0.0;

            return Ok(new
            {
                success = true,
                simulation = new
                {
                    id = simulation.Id,
                    name = simulation.Name,
                    status = (int)simulation.Status,
                    currentStep = 0,
                    isRunning = false,
                    startTime = simulation.StartedAt,
                    endTime = simulation.FinishedAt,
                    totalBurnedCells = 0,
                    totalBurningCells = 0,
                    fireArea = 0,
                    averageSpreadSpeed = 0,
                    graphType = simulation.Parameters.GraphType,
                    precipitation = precipitation
                },
                warning = isInterruptedRunning
                    ? "Симуляция была прервана. Продолжить выполнение шагов нельзя, но можно сделать перезапуск."
                    : null,
                weather = simulation.WeatherCondition != null
                    ? BuildWeatherDto(simulation.WeatherCondition)
                    : null,
                graph = graph2 != null ? new
                {
                    width = graph2.Width,
                    height = graph2.Height,
                    totalCells = graph2.Cells.Count,
                    totalEdges = graph2.Edges.Count
                } : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статуса симуляции {SimulationId}", simulationId);
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка при получении статуса симуляции",
                error = ex.Message
            });
        }
    }

    private object BuildWeatherDto(WeatherCondition weather)
    {
        return new
        {
            temperature = weather.Temperature,
            humidity = weather.Humidity,
            windSpeed = weather.WindSpeedMps,
            windDirectionDegrees = weather.WindDirectionDegrees,
            windDirection = weather.WindDirection.ToString(),
            precipitation = weather.Precipitation
        };
    }

    private object BuildActiveSimulationStartDto(ActiveSimulation activeSimulation, WeatherCondition weather)
    {
        return new
        {
            step = activeSimulation.CurrentStep,
            burningCells = activeSimulation.TotalBurningCells,
            burnedCells = activeSimulation.TotalBurnedCells,
            isRunning = activeSimulation.IsRunning,
            fireArea = activeSimulation.FireArea,
            status = (int)activeSimulation.Simulation.Status,
            weather = BuildWeatherDto(weather)
        };
    }

    private object BuildBasicCellDto(ForestCell c)
    {
        return new
        {
            id = c.Id,
            x = c.X,
            y = c.Y,
            vegetation = c.Vegetation.ToString(),
            moisture = Math.Round(c.Moisture, 2),
            elevation = Math.Round(c.Elevation, 1),
            state = c.State.ToString(),
            burnProbability = Math.Round(c.BurnProbability, 3),
            ignitionTime = c.IgnitionTime,
            burnoutTime = c.BurnoutTime
        };
    }

    private object BuildStepCellDto(ForestCell c)
    {
        return new
        {
            id = c.Id,
            x = c.X,
            y = c.Y,
            vegetation = c.Vegetation.ToString(),
            moisture = Math.Round(c.Moisture, 2),
            elevation = Math.Round(c.Elevation, 1),
            state = c.State.ToString(),
            burnProbability = Math.Round(c.BurnProbability, 3)
        };
    }

    private object BuildRichCellDto(ForestCell c)
    {
        return new
        {
            id = c.Id,
            x = c.X,
            y = c.Y,
            vegetation = c.Vegetation.ToString(),
            moisture = Math.Round(c.Moisture, 2),
            elevation = Math.Round(c.Elevation, 1),
            state = c.State.ToString(),
            burnProbability = Math.Round(c.BurnProbability, 3),
            ignitionTime = c.IgnitionTime,
            burnoutTime = c.BurnoutTime,
            fireStage = c.FireStage.ToString(),
            fireIntensity = Math.Round(c.FireIntensity, 3),
            currentFuelLoad = Math.Round(c.CurrentFuelLoad, 6),
            fuelLoad = Math.Round(c.FuelLoad, 6),
            burningElapsedSeconds = Math.Round(c.BurningElapsedSeconds, 3),
            accumulatedHeatJ = Math.Round(c.AccumulatedHeatJ, 3)
        };
    }

    private object BuildPreparedCellDto(ForestCell c)
    {
        return new
        {
            id = c.Id,
            x = c.X,
            y = c.Y,
            vegetation = c.Vegetation.ToString(),
            moisture = Math.Round(c.Moisture, 2),
            elevation = Math.Round(c.Elevation, 1),
            state = c.State.ToString(),
            burnProbability = Math.Round(c.BurnProbability, 3),
            ignitionTime = c.IgnitionTime,
            burnoutTime = c.BurnoutTime,
            fireStage = c.FireStage.ToString(),
            fireIntensity = Math.Round(c.FireIntensity, 3),
            currentFuelLoad = Math.Round(c.CurrentFuelLoad, 6),
            fuelLoad = Math.Round(c.FuelLoad, 6),
            burningElapsedSeconds = Math.Round(c.BurningElapsedSeconds, 3),
            accumulatedHeatJ = Math.Round(c.AccumulatedHeatJ, 3),
            isIgnitable = c.Vegetation != VegetationType.Water && c.Vegetation != VegetationType.Bare
        };
    }

    private object BuildResetCellDto(ForestCell c)
    {
        return new
        {
            id = c.Id,
            x = c.X,
            y = c.Y,
            vegetation = c.Vegetation.ToString(),
            moisture = Math.Round(c.Moisture, 2),
            elevation = Math.Round(c.Elevation, 1),
            state = c.State.ToString(),
            burnProbability = Math.Round(c.BurnProbability, 3),
            ignitionTime = c.IgnitionTime,
            burnoutTime = c.BurnoutTime,
            isIgnitable = c.Vegetation != VegetationType.Water && c.Vegetation != VegetationType.Bare,
            isSelectedIgnition = false,
            fireStage = c.FireStage.ToString(),
            fireIntensity = Math.Round(c.FireIntensity, 3),
            currentFuelLoad = Math.Round(c.CurrentFuelLoad, 6),
            fuelLoad = Math.Round(c.FuelLoad, 6),
            burningElapsedSeconds = Math.Round(c.BurningElapsedSeconds, 3),
            accumulatedHeatJ = Math.Round(c.AccumulatedHeatJ, 3)
        };
    }

    [HttpGet("{simulationId}/graph")]
    public async Task<IActionResult> GetSimulationGraph(Guid simulationId, CancellationToken cancellationToken)
    {
        try
        {
            var activeSimulation = await _simulationManager.GetSimulation(simulationId);

            Simulation? simulation = null;
            ForestGraph? graph = null;

            if (activeSimulation != null)
            {
                simulation = activeSimulation.Simulation;
                graph = activeSimulation.Graph;
            }
            else
            {
                simulation = await _context.Simulations
                    .Include(s => s.Parameters)
                    .FirstOrDefaultAsync(s => s.Id == simulationId, cancellationToken);

                if (simulation != null)
                {
                    graph = await GetOrCreatePersistedGraphAsync(simulation, cancellationToken);
                }
            }

            if (simulation == null || graph == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Граф симуляции {simulationId} не найден"
                });
            }

            var dto = BuildGraphDto(simulation, graph);

            return Ok(new
            {
                success = true,
                graph = dto
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении полного графа симуляции {SimulationId}", simulationId);
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка при получении графа симуляции",
                error = ex.Message
            });
        }
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveSimulations()
    {
        try
        {
            var activeSimulations = await _simulationManager.GetAllSimulations();

            return Ok(new
            {
                success = true,
                count = activeSimulations.Count,
                simulations = activeSimulations.Select(s => new
                {
                    id = s.Simulation.Id,
                    name = s.Simulation.Name,
                    currentStep = s.CurrentStep,
                    isRunning = s.IsRunning,
                    status = (int)s.Simulation.Status,
                    graphType = s.Simulation.Parameters.GraphType,
                    burningCells = s.TotalBurningCells,
                    burnedCells = s.TotalBurnedCells,
                    weather = new
                    {
                        windSpeed = s.CurrentWeather.WindSpeedMps,
                        windDirection = s.CurrentWeather.WindDirection.ToString(),
                        temperature = s.CurrentWeather.Temperature
                    }
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении списка активных симуляций");
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка при получении списка активных симуляций",
                error = ex.Message
            });
        }
    }

    [HttpDelete("{simulationId}")]
    public async Task<IActionResult> DeleteSimulation(Guid simulationId)
    {
        try
        {
            var deleted = await _simulationManager.DeleteSimulation(simulationId);

            if (!deleted)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Симуляция {simulationId} не найдена"
                });
            }

            return Ok(new
            {
                success = true,
                message = $"Симуляция {simulationId} удалена"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении симуляции {SimulationId}", simulationId);
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка при удалении симуляции",
                error = ex.Message
            });
        }
    }

    [HttpGet("{simulationId}/cells")]
    public async Task<IActionResult> GetSimulationCells(
        Guid simulationId,
        [FromQuery] int? x = null,
        [FromQuery] int? y = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var activeSimulation = await _simulationManager.GetSimulation(simulationId);

            ForestGraph? graph = null;

            if (activeSimulation != null)
            {
                graph = activeSimulation.Graph;
            }
            else
            {
                var simulation = await _context.Simulations
                    .Include(s => s.Parameters)
                    .FirstOrDefaultAsync(s => s.Id == simulationId, cancellationToken);

                if (simulation != null)
                {
                    graph = await GetOrCreatePersistedGraphAsync(simulation, cancellationToken);
                    if (graph != null)
                    {
                        _logger.LogInformation(
                            "📦 Загружен или сгенерирован граф из БД для симуляции {SimulationId}",
                            simulationId);
                    }
                }
            }

            if (graph == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Симуляция {simulationId} не найдена и не имеет сохраненного графа"
                });
            }

            var cells = graph.Cells.AsEnumerable();

            if (x.HasValue)
                cells = cells.Where(c => c.X == x.Value);

            if (y.HasValue)
                cells = cells.Where(c => c.Y == y.Value);

            var cellDtos = cells
                .Select(BuildBasicCellDto)
                .ToList();

            return Ok(new
            {
                success = true,
                count = cellDtos.Count,
                cells = cellDtos
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении клеток симуляции {SimulationId}", simulationId);
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка при получении клеток симуляции",
                error = ex.Message
            });
        }
    }

    private async Task<ForestGraph?> GetOrCreatePersistedGraphAsync(
        Simulation simulation,
        CancellationToken cancellationToken)
    {
        if (simulation.HasSavedGraph())
        {
            simulation.CachedGraph = null;
            var savedGraph = simulation.LoadGraph();

            if (savedGraph != null && simulation.Parameters.StepDurationSeconds > 0)
                savedGraph.StepDurationSeconds = simulation.Parameters.StepDurationSeconds;

            return savedGraph;
        }

        if (simulation.Parameters == null)
        {
            _logger.LogWarning(
                "У симуляции {SimulationId} отсутствуют параметры, граф сгенерировать нельзя",
                simulation.Id);
            return null;
        }

        _logger.LogInformation(
            "🛠 Для симуляции {SimulationId} граф ещё не сохранён. Генерируем его on-demand.",
            simulation.Id);

        ForestGraph graph = await GenerateFreshGraphAsync(simulation);

        graph.StepDurationSeconds = simulation.Parameters.StepDurationSeconds > 0
            ? simulation.Parameters.StepDurationSeconds
            : 60;

        simulation.SaveGraph(graph);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "💾 Для симуляции {SimulationId} граф был сгенерирован и сохранён",
            simulation.Id);

        return graph;
    }

    [HttpPost("{simulationId}/weather")]
    public async Task<IActionResult> UpdateWeather(
        Guid simulationId,
        [FromBody] UpdateWeatherDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Обновление погоды для симуляции {SimulationId}", simulationId);

            var simulation = await _context.Simulations
                .FirstOrDefaultAsync(s => s.Id == simulationId, cancellationToken);

            if (simulation == null)
            {
                return NotFound(new { message = $"Симуляция {simulationId} не найдена" });
            }

            var newWeather = new WeatherCondition(
                DateTime.UtcNow,
                dto.Temperature,
                dto.Humidity,
                dto.WindSpeed,
                dto.WindDirectionDegrees,
                dto.Precipitation
            );

            await _context.WeatherConditions.AddAsync(newWeather, cancellationToken);
            simulation.SetWeatherCondition(newWeather);

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Погода обновлена: {Temp}°C, ветер {WindSpeed} м/с, направление {WindDir}°",
                newWeather.Temperature,
                newWeather.WindSpeedMps,
                newWeather.WindDirectionDegrees);

            return Ok(new
            {
                success = true,
                message = "Погода обновлена",
                weather = new
                {
                    temperature = newWeather.Temperature,
                    humidity = newWeather.Humidity,
                    windSpeed = newWeather.WindSpeedMps,
                    windDirectionDegrees = newWeather.WindDirectionDegrees,
                    windDirection = newWeather.WindDirection.ToString(),
                    precipitation = newWeather.Precipitation,
                    timestamp = newWeather.Timestamp
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении погоды для симуляции {SimulationId}", simulationId);
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка при обновлении погоды",
                error = ex.Message
            });
        }
    }

    [HttpPost("{simulationId}/reset")]
    public async Task<IActionResult> ResetSimulation(Guid simulationId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🔄 Запрос на перезапуск симуляции {SimulationId}", simulationId);

            var simulation = await _context.Simulations
                .Include(s => s.Parameters)
                .Include(s => s.WeatherCondition)
                .FirstOrDefaultAsync(s => s.Id == simulationId, cancellationToken);

            if (simulation == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Симуляция {simulationId} не найдена"
                });
            }

            if (!simulation.HasSavedGraph())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Граф для этой симуляции не сохранен. Перезапуск возможен только для симуляции с подготовленной картой."
                });
            }

            var resetSuccess = await _simulationManager.ResetSimulationAsync(simulationId);

            if (!resetSuccess)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Ошибка при перезапуске симуляции"
                });
            }

            var oldMetrics = await _context.FireMetrics
                .Where(m => m.SimulationId == simulationId)
                .ToListAsync(cancellationToken);

            if (oldMetrics.Count > 0)
            {
                _context.FireMetrics.RemoveRange(oldMetrics);
            }

            var oldActiveRecord = await _context.ActiveSimulationRecords
                .FirstOrDefaultAsync(r => r.SimulationId == simulationId, cancellationToken);

            if (oldActiveRecord != null)
            {
                _context.ActiveSimulationRecords.Remove(oldActiveRecord);
            }

            simulation.ResetForRestart();

            simulation.CachedGraph = null;

            await _context.SaveChangesAsync(cancellationToken);

            var preparedGraph = simulation.LoadGraph();
            var cells = preparedGraph?.Cells
                .Select(BuildResetCellDto)
                .ToList() ?? new List<object>();

            return Ok(new
            {
                success = true,
                message = $"Симуляция {simulation.Name} перезапущена",
                simulationId = simulation.Id,
                graphType = simulation.Parameters.GraphType,
                activeSimulation = new
                {
                    step = 0,
                    burningCells = 0,
                    burnedCells = 0,
                    isRunning = false,
                    fireArea = 0.0,
                    status = (int)SimulationStatus.Created,
                    weather = simulation.WeatherCondition == null
                        ? null
                        : BuildWeatherDto(simulation.WeatherCondition)
                },
                cells = cells
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при перезапуске симуляции {SimulationId}", simulationId);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }
    [HttpPost("{simulationId}/refresh-ignitions")]
    public async Task<IActionResult> RefreshIgnitions(Guid simulationId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🔁 Запрос на обновление стартовых очагов для симуляции {SimulationId}", simulationId);

            var simulation = await _context.Simulations
                .Include(s => s.Parameters)
                .FirstOrDefaultAsync(s => s.Id == simulationId, cancellationToken);

            if (simulation == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Симуляция {simulationId} не найдена"
                });
            }

            if (simulation.Status != SimulationStatus.Created)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Обновить очаги можно только у симуляции в статусе Created."
                });
            }

            var graph = await _simulationManager.RefreshIgnitionSetupAsync(simulationId);

            if (simulation.Parameters.GraphType == GraphType.Grid)
            {
                var cells = graph.Cells
                    .Select(BuildPreparedCellDto)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    message = "Сохранённые очаги очищены. Можно выбрать новые.",
                    simulationId = simulation.Id,
                    graphType = simulation.Parameters.GraphType,
                    width = graph.Width,
                    height = graph.Height,
                    cells = cells
                });
            }

            var graphDto = BuildGraphDto(simulation, graph);

            return Ok(new
            {
                success = true,
                message = "Сохранённые очаги очищены. Можно выбрать новые.",
                simulationId = simulation.Id,
                graphType = simulation.Parameters.GraphType,
                graph = graphDto
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Ошибка обновления стартовых очагов для симуляции {SimulationId}", simulationId);
            return BadRequest(new
            {
                success = false,
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении стартовых очагов симуляции {SimulationId}", simulationId);
            return StatusCode(500, new
            {
                success = false,
                message = "Ошибка при обновлении стартовых очагов",
                error = ex.Message
            });
        }
    }

    private async Task<ForestGraph> GenerateFreshGraphAsync(Simulation simulation)
    {
        return simulation.Parameters.GraphType switch
        {
            GraphType.Grid => await _graphGenerator.GenerateGridAsync(
                simulation.Parameters.GridWidth,
                simulation.Parameters.GridHeight,
                simulation.Parameters),

            GraphType.ClusteredGraph => await _graphGenerator.GenerateClusteredGraphAsync(
                GetGraphNodeCountForScale(simulation.Parameters),
                simulation.Parameters),

            _ => await _graphGenerator.GenerateGridAsync(
                simulation.Parameters.GridWidth,
                simulation.Parameters.GridHeight,
                simulation.Parameters)
        };
    }

    private int GetGraphNodeCountForScale(SimulationParameters parameters)
    {
        if (parameters.ClusteredBlueprint != null &&
            parameters.ClusteredBlueprint.Nodes.Any())
        {
            return parameters.ClusteredBlueprint.Nodes.Count;
        }

        var scale = parameters.GraphScaleType ?? GraphScaleType.Medium;

        return scale switch
        {
            GraphScaleType.Small => 20,
            GraphScaleType.Medium => 70,
            GraphScaleType.Large => 140,
            _ => 70
        };
    }
    private SimulationGraphDto BuildGraphDto(Simulation simulation, ForestGraph graph)
    {
        var nodeDtos = BuildNodeDtos(simulation, graph);
        var nodeMap = nodeDtos.ToDictionary(n => n.Id);
        var domainNodeMap = graph.Cells.ToDictionary(c => c.Id);

        return new SimulationGraphDto
        {
            SimulationId = simulation.Id,
            SimulationName = simulation.Name,
            GraphType = simulation.Parameters.GraphType,
            GraphScaleType = simulation.Parameters.GraphScaleType,
            LayoutHint = GetLayoutHint(simulation.Parameters),
            Width = graph.Width,
            Height = graph.Height,
            StepDurationSeconds = graph.StepDurationSeconds,
            Nodes = nodeDtos,
            Edges = graph.Edges
                .Where(e => nodeMap.ContainsKey(e.FromCellId)
                         && nodeMap.ContainsKey(e.ToCellId)
                         && domainNodeMap.ContainsKey(e.FromCellId)
                         && domainNodeMap.ContainsKey(e.ToCellId))
                .Select(e => new SimulationGraphEdgeDto
                {
                    Id = e.Id,
                    FromCellId = e.FromCellId,
                    FromX = domainNodeMap[e.FromCellId].X,
                    FromY = domainNodeMap[e.FromCellId].Y,
                    FromRenderX = nodeMap[e.FromCellId].RenderX,
                    FromRenderY = nodeMap[e.FromCellId].RenderY,
                    ToCellId = e.ToCellId,
                    ToX = domainNodeMap[e.ToCellId].X,
                    ToY = domainNodeMap[e.ToCellId].Y,
                    ToRenderX = nodeMap[e.ToCellId].RenderX,
                    ToRenderY = nodeMap[e.ToCellId].RenderY,
                    Distance = Math.Round(e.Distance, 6),
                    Slope = Math.Round(e.Slope, 6),
                    FireSpreadModifier = Math.Round(e.FireSpreadModifier, 6),
                    AccumulatedHeat = Math.Round(e.AccumulatedHeat, 3),
                    IsCorridor = e.IsCorridor
                })
                .ToList()
        };
    }
    private List<SimulationGraphNodeDto> BuildNodeDtos(Simulation simulation, ForestGraph graph)
    {
        return simulation.Parameters.GraphType switch
        {
            GraphType.ClusteredGraph => BuildClusteredGraphNodeDtos(graph),
            _ => BuildGridNodeDtos(graph)
        };
    }

    private List<SimulationGraphNodeDto> BuildGridNodeDtos(ForestGraph graph)
    {
        return graph.Cells
            .Select(c => new SimulationGraphNodeDto
            {
                Id = c.Id,
                X = c.X,
                Y = c.Y,
                RenderX = c.X,
                RenderY = c.Y,
                GroupKey = $"row-{c.Y}",
                Vegetation = c.Vegetation.ToString(),
                Moisture = Math.Round(c.Moisture, 3),
                Elevation = Math.Round(c.Elevation, 3),
                State = c.State.ToString(),
                BurnProbability = Math.Round(c.BurnProbability, 6),
                IgnitionTime = c.IgnitionTime,
                BurnoutTime = c.BurnoutTime,
                FireStage = c.FireStage.ToString(),
                FireIntensity = Math.Round(c.FireIntensity, 3),
                CurrentFuelLoad = Math.Round(c.CurrentFuelLoad, 6),
                FuelLoad = Math.Round(c.FuelLoad, 6),
                BurningElapsedSeconds = Math.Round(c.BurningElapsedSeconds, 3),
                AccumulatedHeatJ = Math.Round(c.AccumulatedHeatJ, 3),
                IsIgnitable = c.Vegetation != VegetationType.Water && c.Vegetation != VegetationType.Bare
            })
            .ToList();
    }

    private List<SimulationGraphNodeDto> BuildClusteredGraphNodeDtos(ForestGraph graph)
    {
        return graph.Cells
            .Select(c => new SimulationGraphNodeDto
            {
                Id = c.Id,
                X = c.X,
                Y = c.Y,
                RenderX = c.X,
                RenderY = c.Y,
                GroupKey = !string.IsNullOrWhiteSpace(c.ClusterId)
                    ? c.ClusterId
                    : GetVegetationGroupKey(c),
                Vegetation = c.Vegetation.ToString(),
                Moisture = Math.Round(c.Moisture, 3),
                Elevation = Math.Round(c.Elevation, 3),
                State = c.State.ToString(),
                BurnProbability = Math.Round(c.BurnProbability, 6),
                IgnitionTime = c.IgnitionTime,
                BurnoutTime = c.BurnoutTime,
                FireStage = c.FireStage.ToString(),
                FireIntensity = Math.Round(c.FireIntensity, 3),
                CurrentFuelLoad = Math.Round(c.CurrentFuelLoad, 6),
                FuelLoad = Math.Round(c.FuelLoad, 6),
                BurningElapsedSeconds = Math.Round(c.BurningElapsedSeconds, 3),
                AccumulatedHeatJ = Math.Round(c.AccumulatedHeatJ, 3),
                IsIgnitable = c.Vegetation != VegetationType.Water && c.Vegetation != VegetationType.Bare
            })
            .ToList();
    }

    private string GetLayoutHint(SimulationParameters parameters)
    {
        if (parameters.GraphType == GraphType.Grid)
            return "grid";

        var scale = parameters.GraphScaleType ?? GraphScaleType.Medium;

        return scale switch
        {
            GraphScaleType.Small => "graph-small",
            GraphScaleType.Medium => "graph-medium",
            GraphScaleType.Large => "graph-large",
            _ => "graph-medium"
        };
    }

    private string GetVegetationGroupKey(ForestCell cell)
    {
        return $"veg-{cell.Vegetation}";
    }
}

public class UpdateWeatherDto
{
    public double Temperature { get; set; } = 25.0;
    public double Humidity { get; set; } = 40.0;
    public double WindSpeed { get; set; } = 5.0;
    public double WindDirectionDegrees { get; set; } = 45.0;
    public double Precipitation { get; set; } = 0.0;
}

public class StartSimulationRequest
{
    public string IgnitionMode { get; set; } = "saved-or-random";
    public List<InitialFirePositionDto> InitialFirePositions { get; set; } = new();
}