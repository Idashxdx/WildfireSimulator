using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;
using WildfireSimulator.Application.Interfaces;
using WildfireSimulator.Domain.Models;
using WildfireSimulator.Application.Models.Events;

namespace WildfireSimulator.Application.Services;

public partial class SimulationManager
{
    private readonly ILogger<SimulationManager> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaFacade _kafkaFacade;
    private readonly Dictionary<Guid, ActiveSimulation> _activeSimulations = new();
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SimulationManager(
        ILogger<SimulationManager> logger,
        IServiceScopeFactory scopeFactory,
        KafkaFacade kafkaFacade)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _kafkaFacade = kafkaFacade;
        _logger.LogInformation("=== SIMULATION MANAGER СОЗДАН ===");
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;
            _logger.LogInformation("Начинаем инициализацию SimulationManager...");
            await LoadActiveSimulationsFromDbAsync();
            _isInitialized = true;
            _logger.LogInformation("SimulationManager инициализирован. Загружено {Count} симуляций", _activeSimulations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации SimulationManager");
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_isInitialized)
            await InitializeAsync();
    }

    private async Task LoadActiveSimulationsFromDbAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var activeSimulationRepo = scope.ServiceProvider.GetService<IActiveSimulationRepository>();
        var simulationRepo = scope.ServiceProvider.GetService<ISimulationRepository>();

        if (activeSimulationRepo == null)
        {
            _logger.LogWarning("IActiveSimulationRepository не зарегистрирован в DI");
            return;
        }

        if (simulationRepo == null)
        {
            _logger.LogWarning("ISimulationRepository не зарегистрирован в DI");
            return;
        }

        try
        {
            var activeRecords = await activeSimulationRepo.GetAllActiveAsync();
            _logger.LogInformation("Найдено {Count} активных записей в БД", activeRecords.Count());

            foreach (var record in activeRecords)
            {
                try
                {
                    var simulation = await simulationRepo.GetByIdAsync(record.SimulationId);
                    if (simulation == null)
                    {
                        _logger.LogWarning("Симуляция {SimulationId} не найдена в БД, удаляем stale active-record", record.SimulationId);
                        await activeSimulationRepo.DeleteBySimulationIdAsync(record.SimulationId);
                        continue;
                    }

                    if (!record.IsRunning || simulation.Status != SimulationStatus.Running)
                    {
                        _logger.LogInformation(
                            "Пропускаем восстановление симуляции {SimulationId}: record.IsRunning={IsRunning}, simulation.Status={Status}. Удаляем active-record.",
                            record.SimulationId,
                            record.IsRunning,
                            simulation.Status);

                        await activeSimulationRepo.DeleteBySimulationIdAsync(record.SimulationId);
                        continue;
                    }

                    var activeSimulation = await RestoreSimulationFromRecordAsync(record);
                    if (activeSimulation != null)
                    {
                        _activeSimulations[record.SimulationId] = activeSimulation;
                        _logger.LogInformation(
                            "Восстановлена активная симуляция {SimulationId} (шаг {Step})",
                            record.SimulationId, record.CurrentStep);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Не удалось восстановить активную симуляцию {SimulationId}, удаляем active-record",
                            record.SimulationId);

                        await activeSimulationRepo.DeleteBySimulationIdAsync(record.SimulationId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при восстановлении симуляции {SimulationId}", record.SimulationId);
                }
            }

            _logger.LogInformation("Загружено {Count} активных симуляций из БД", _activeSimulations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке активных симуляций из БД");
        }
    }

    private async Task<ActiveSimulation?> RestoreSimulationFromRecordAsync(ActiveSimulationRecord record)
    {
        using var scope = _scopeFactory.CreateScope();

        try
        {
            var simulationRepo = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();
            var simulation = await simulationRepo.GetByIdAsync(record.SimulationId);

            if (simulation == null)
            {
                _logger.LogWarning("Симуляция {SimulationId} не найдена в БД", record.SimulationId);
                return null;
            }

            var graph = simulation.LoadGraph();
            if (graph == null)
            {
                _logger.LogWarning("Не удалось восстановить граф из Simulation.SerializedGraph для симуляции {SimulationId}", record.SimulationId);
                return null;
            }

            if (simulation.Parameters.StepDurationSeconds > 0)
                graph.StepDurationSeconds = simulation.Parameters.StepDurationSeconds;

            var weather = JsonSerializer.Deserialize<WeatherCondition>(record.WeatherData, _jsonOptions);
            if (weather == null)
            {
                if (simulation.WeatherCondition != null)
                {
                    weather = simulation.WeatherCondition;
                }
                else
                {
                    _logger.LogWarning("Не удалось десериализовать погоду для симуляции {SimulationId}", record.SimulationId);
                    return null;
                }
            }

            var stepResults = JsonSerializer.Deserialize<List<SimulationStepResult>>(record.StepResultsData, _jsonOptions) ?? new();

            return new ActiveSimulation
            {
                Simulation = simulation,
                Graph = graph,
                CurrentWeather = weather,
                CurrentStep = record.CurrentStep,
                IsRunning = record.IsRunning,
                StartTime = record.StartTime,
                EndTime = record.EndTime,
                TotalBurnedCells = record.TotalBurnedCells,
                TotalBurningCells = record.TotalBurningCells,
                StepResults = stepResults
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при восстановлении симуляции {SimulationId}", record.SimulationId);
            return null;
        }
    }

    private async Task SaveSimulationToDbAsync(ActiveSimulation activeSimulation)
    {
        using var scope = _scopeFactory.CreateScope();
        var activeSimulationRepo = scope.ServiceProvider.GetService<IActiveSimulationRepository>();
        if (activeSimulationRepo == null)
        {
            _logger.LogWarning("IActiveSimulationRepository не доступен для сохранения");
            return;
        }

        try
        {
            if (!activeSimulation.IsRunning || activeSimulation.Simulation.Status != SimulationStatus.Running)
            {
                await activeSimulationRepo.DeleteBySimulationIdAsync(activeSimulation.Simulation.Id);
                _logger.LogDebug(
                    "Active-record для симуляции {SimulationId} удалён, так как симуляция больше не Running",
                    activeSimulation.Simulation.Id);
                return;
            }

            var weatherData = JsonSerializer.Serialize(activeSimulation.CurrentWeather, _jsonOptions);
            var stepResultsData = JsonSerializer.Serialize(activeSimulation.StepResults, _jsonOptions);

            var existingRecord = await activeSimulationRepo.GetBySimulationIdAsync(activeSimulation.Simulation.Id);

            if (existingRecord == null)
            {
                var record = new ActiveSimulationRecord(
                    activeSimulation.Simulation.Id,
                    activeSimulation.Simulation.Name,
                    activeSimulation.CurrentStep,
                    activeSimulation.IsRunning,
                    activeSimulation.StartTime,
                    activeSimulation.EndTime,
                    activeSimulation.TotalBurnedCells,
                    activeSimulation.TotalBurningCells,
                    weatherData,
                    stepResultsData
                );

                await activeSimulationRepo.AddAsync(record);
            }
            else
            {
                existingRecord.Update(
                    activeSimulation.CurrentStep,
                    activeSimulation.IsRunning,
                    activeSimulation.EndTime,
                    activeSimulation.TotalBurnedCells,
                    activeSimulation.TotalBurningCells,
                    weatherData,
                    stepResultsData
                );

                await activeSimulationRepo.UpdateAsync(existingRecord);
            }

            _logger.LogDebug("Симуляция {SimulationId} сохранена в БД (шаг {Step})",
                activeSimulation.Simulation.Id, activeSimulation.CurrentStep);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении симуляции {SimulationId} в БД",
                activeSimulation.Simulation.Id);
        }
    }

    private async Task DeleteSimulationFromDbAsync(Guid simulationId)
    {
        using var scope = _scopeFactory.CreateScope();
        var activeSimulationRepo = scope.ServiceProvider.GetService<IActiveSimulationRepository>();
        if (activeSimulationRepo == null)
        {
            _logger.LogWarning("IActiveSimulationRepository не доступен для удаления");
            return;
        }

        try
        {
            await activeSimulationRepo.DeleteBySimulationIdAsync(simulationId);
            _logger.LogDebug("Симуляция {SimulationId} удалена из active-таблицы БД", simulationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении симуляции {SimulationId} из БД", simulationId);
        }
    }

    public Task<ActiveSimulation> StartSimulationAsync(
     Simulation simulation,
     WeatherCondition weather)
    {
        return StartSimulationAsync(
            simulation,
            weather,
            initialFirePositions: null);
    }

    public async Task<ActiveSimulation> StartSimulationAsync(
        Simulation simulation,
        WeatherCondition weather,
        List<(int X, int Y)>? initialFirePositions)
    {
        await EnsureInitializedAsync();

        if (simulation.Status != SimulationStatus.Created)
        {
            throw new InvalidOperationException(
                $"Симуляцию со статусом {simulation.Status} нельзя запускать. Создайте новую симуляцию или используйте перезапуск.");
        }

        if (_activeSimulations.TryGetValue(simulation.Id, out var existingSimulation) && existingSimulation.IsRunning)
        {
            throw new InvalidOperationException("Симуляция уже запущена.");
        }

        _activeSimulations.Remove(simulation.Id);

        _logger.LogInformation(
            "Запуск симуляции {SimulationId} ({Name}) с параметрами {Width}x{Height}",
            simulation.Id, simulation.Name, simulation.Parameters.GridWidth, simulation.Parameters.GridHeight);

        using var scope = _scopeFactory.CreateScope();
        var graphGenerator = scope.ServiceProvider.GetRequiredService<IForestGraphGenerator>();
        var fireSpreadSimulator = scope.ServiceProvider.GetRequiredService<IFireSpreadSimulator>();
        var simulationRepo = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();

        ForestGraph graph;

        if (simulation.HasSavedGraph())
        {
            _logger.LogInformation("📦 Используем сохранённый граф для симуляции {SimulationId}", simulation.Id);

            graph = simulation.LoadGraph()
                ?? throw new InvalidOperationException($"Не удалось загрузить граф для симуляции {simulation.Id}");
        }
        else
        {
            _logger.LogInformation("🆕 Сохранённого графа нет, генерируем новый для симуляции {SimulationId}", simulation.Id);

            graph = await CreateBaseGraphAsync(simulation, graphGenerator);

            simulation.SaveGraph(graph);
            await simulationRepo.UpdateAsync(simulation);

            _logger.LogInformation("💾 Базовый граф без очагов сохранён для симуляции {SimulationId}", simulation.Id);
        }

        graph.StepDurationSeconds = simulation.Parameters.StepDurationSeconds > 0
            ? simulation.Parameters.StepDurationSeconds
            : 60;

        ResetGraphForReplay(graph);

        var savedInitialFirePositions = simulation.LoadInitialFirePositions();

        var validatedManualPositions = GetValidatedIgnitionPositions(graph, initialFirePositions);
        var validatedSavedPositions = GetValidatedIgnitionPositions(graph, savedInitialFirePositions);

        List<(int X, int Y)> ignitionPositions;

        if (validatedManualPositions.Any())
        {
            ignitionPositions = validatedManualPositions;
            _logger.LogInformation(
                "- Запуск с ручными очагами: {Count} клеток",
                ignitionPositions.Count);
        }
        else if (validatedSavedPositions.Any())
        {
            ignitionPositions = validatedSavedPositions;
            _logger.LogInformation(
                "- Запуск с сохранёнными очагами: {Count} клеток",
                ignitionPositions.Count);
        }
        else
        {
            ignitionPositions = new List<(int X, int Y)>();
            _logger.LogInformation(
                "🎲 Запуск со случайной генерацией очагов: requested={Count}",
                simulation.Parameters.InitialFireCellsCount);
        }

        ForestGraph graphWithFire;

        if (ignitionPositions.Any())
        {
            graphWithFire = await fireSpreadSimulator.InitializeFireAsync(
                graph,
                ignitionPositions.Count,
                weather,
                ignitionPositions);
        }
        else
        {
            graphWithFire = await fireSpreadSimulator.InitializeFireAsync(
                graph,
                simulation.Parameters.InitialFireCellsCount,
                weather,
                null);
        }

        var burningCells = graphWithFire.Cells.Where(c => c.State == CellState.Burning).ToList();
        var persistedPositions = burningCells
            .Select(c => (c.X, c.Y))
            .Distinct()
            .ToList();

        simulation.SaveInitialFirePositions(persistedPositions);
        simulation.SaveGraph(graphWithFire);

        _logger.LogInformation(
            "💾 Сохранены стартовые позиции очагов: {Count} для симуляции {SimulationId}",
            persistedPositions.Count,
            simulation.Id);

        simulation.Start(weather);
        await simulationRepo.UpdateAsync(simulation);

        var burningCellsCount = burningCells.Count;

        _logger.LogInformation("🔥 Начальный пожар инициализирован на {BurningCells} клетках", burningCellsCount);

        _logger.LogInformation("📤 Отправка SimulationStart в Kafka для {SimulationId}", simulation.Id);
        await _kafkaFacade.SendSimulationStart(
            simulation.Id,
            simulation.Name,
            simulation.Parameters.GridWidth,
            simulation.Parameters.GridHeight,
            simulation.Parameters.InitialFireCellsCount,
            burningCellsCount);

        var activeSimulation = new ActiveSimulation
        {
            Simulation = simulation,
            Graph = graphWithFire,
            CurrentWeather = weather,
            CurrentStep = 0,
            IsRunning = true,
            StartTime = DateTime.UtcNow,
            TotalBurnedCells = 0,
            TotalBurningCells = burningCellsCount
        };

        _activeSimulations[simulation.Id] = activeSimulation;
        await SaveSimulationToDbAsync(activeSimulation);

        _logger.LogInformation(
            "Симуляция {SimulationId} запущена. Горящих клеток: {BurningCells}, Всего активных симуляций: {Total}",
            simulation.Id, burningCellsCount, _activeSimulations.Count);

        return activeSimulation;
    }
    public async Task<ForestGraph> PrepareSimulationGraphAsync(Guid simulationId)
    {
        await EnsureInitializedAsync();

        using var scope = _scopeFactory.CreateScope();
        var simulationRepo = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();
        var graphGenerator = scope.ServiceProvider.GetRequiredService<IForestGraphGenerator>();

        var simulation = await simulationRepo.GetByIdAsync(simulationId);
        if (simulation == null)
            throw new InvalidOperationException($"Симуляция {simulationId} не найдена");

        if (simulation.Status != SimulationStatus.Created)
            throw new InvalidOperationException("Подготовить карту можно только для симуляции в статусе Created.");

        ForestGraph graph;

        if (simulation.HasSavedGraph())
        {
            graph = simulation.LoadGraph()
                ?? throw new InvalidOperationException($"Не удалось загрузить граф для симуляции {simulationId}");

            _logger.LogInformation(
                "📦 Для симуляции {SimulationId} уже есть сохранённый граф, используем его для предпросмотра",
                simulationId);
        }
        else
        {
            graph = await CreateBaseGraphAsync(simulation, graphGenerator);
            simulation.SaveGraph(graph);
            await simulationRepo.UpdateAsync(simulation);

            _logger.LogInformation(
                "💾 Для симуляции {SimulationId} сгенерирован и сохранён граф без очагов",
                simulationId);
        }

        graph.StepDurationSeconds = simulation.Parameters.StepDurationSeconds > 0
            ? simulation.Parameters.StepDurationSeconds
            : 60;

        ResetGraphForReplay(graph);
        simulation.SaveGraph(graph);
        await simulationRepo.UpdateAsync(simulation);

        return graph;
    }
    private async Task<ForestGraph> CreateBaseGraphAsync(
     Simulation simulation,
     IForestGraphGenerator graphGenerator)
    {
        ForestGraph graph = simulation.Parameters.GraphType switch
        {
            GraphType.Grid => await graphGenerator.GenerateGridAsync(
                simulation.Parameters.GridWidth,
                simulation.Parameters.GridHeight,
                simulation.Parameters),

            GraphType.ClusteredGraph => await graphGenerator.GenerateClusteredGraphAsync(
                simulation.Parameters.GridWidth * simulation.Parameters.GridHeight / 2,
                simulation.Parameters),

            _ => await graphGenerator.GenerateGridAsync(
                simulation.Parameters.GridWidth,
                simulation.Parameters.GridHeight,
                simulation.Parameters)
        };

        graph.StepDurationSeconds = simulation.Parameters.StepDurationSeconds > 0
            ? simulation.Parameters.StepDurationSeconds
            : 60;

        return graph;
    }
    private List<(int X, int Y)> GetValidatedIgnitionPositions(
        ForestGraph graph,
        List<(int X, int Y)>? positions)
    {
        var result = new List<(int X, int Y)>();

        if (positions == null || positions.Count == 0)
            return result;

        foreach (var position in positions.Distinct())
        {
            var cell = graph.GetCell(position.X, position.Y);
            if (cell == null)
                continue;

            if (cell.State != CellState.Normal)
                continue;

            if (cell.Vegetation == VegetationType.Water || cell.Vegetation == VegetationType.Bare)
                continue;

            result.Add((cell.X, cell.Y));
        }

        return result;
    }
    public async Task<SimulationStepResult> ExecuteStepAsync(Guid simulationId)
    {
        await EnsureInitializedAsync();

        if (!_activeSimulations.TryGetValue(simulationId, out var activeSimulation))
        {
            _logger.LogError("Симуляция {SimulationId} не найдена. Доступные ID: {@Keys}",
                simulationId, _activeSimulations.Keys);
            throw new InvalidOperationException($"Симуляция {simulationId} не найдена или не запущена");
        }

        if (!activeSimulation.IsRunning)
        {
            throw new InvalidOperationException("Симуляция уже завершена. Выполнять новые шаги нельзя.");
        }

        if (activeSimulation.Simulation.Status != SimulationStatus.Running)
        {
            throw new InvalidOperationException("Симуляция не находится в статусе Running.");
        }

        activeSimulation.CurrentStep++;

        _logger.LogInformation(
            "📢 Выполнение шага {Step} для симуляции {SimulationId}",
            activeSimulation.CurrentStep, simulationId);

        using var scope = _scopeFactory.CreateScope();
        var fireSpreadSimulator = scope.ServiceProvider.GetRequiredService<IFireSpreadSimulator>();
        var simulationRepo = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();

        var configuredStepDurationSeconds =
            activeSimulation.Simulation.Parameters.StepDurationSeconds > 0
                ? activeSimulation.Simulation.Parameters.StepDurationSeconds
                : 60;

        var stepResult = await fireSpreadSimulator.SimulateStepAsync(
            activeSimulation.Graph,
            activeSimulation.CurrentWeather,
            activeSimulation.CurrentStep,
            simulationId,
            configuredStepDurationSeconds);

        activeSimulation.TotalBurnedCells = stepResult.BurnedCellsCount;
        activeSimulation.TotalBurningCells = stepResult.BurningCellsCount;

        _logger.LogInformation(
            "✅ Шаг {Step} выполнен: Горит={Burning}, Сгорело={Burned}, Новых={New}, Площадь={Area:F1} га",
            activeSimulation.CurrentStep,
            stepResult.BurningCellsCount,
            stepResult.BurnedCellsCount,
            stepResult.NewlyIgnitedCells,
            stepResult.FireArea);

        await SendEventsToKafka(stepResult, activeSimulation);
        await SaveStepMetricsAsync(activeSimulation, stepResult);

        if (activeSimulation.CurrentStep >= activeSimulation.Simulation.Parameters.SimulationSteps)
        {
            activeSimulation.IsRunning = false;
            activeSimulation.EndTime = DateTime.UtcNow;

            activeSimulation.Simulation.Finish();
            await simulationRepo.UpdateAsync(activeSimulation.Simulation);

            await _kafkaFacade.SendSimulationEnd(
                simulationId,
                activeSimulation.CurrentStep,
                stepResult.BurnedCellsCount,
                stepResult.FireArea);

            _logger.LogInformation(
                "🏁 Симуляция {SimulationId} завершена по лимиту шагов на шаге {Step}. Всего выгорело: {BurnedCells} клеток, площадь: {Area:F1} га",
                simulationId, activeSimulation.CurrentStep, stepResult.BurnedCellsCount, stepResult.FireArea);
        }
        else if (stepResult.BurningCellsCount == 0)
        {
            activeSimulation.IsRunning = false;
            activeSimulation.EndTime = DateTime.UtcNow;

            activeSimulation.Simulation.Finish();
            await simulationRepo.UpdateAsync(activeSimulation.Simulation);

            await _kafkaFacade.SendSimulationEnd(
                simulationId,
                activeSimulation.CurrentStep,
                stepResult.BurnedCellsCount,
                stepResult.FireArea);

            _logger.LogInformation(
                "🏁 Симуляция {SimulationId} завершена: активных очагов больше нет. Шаг={Step}, площадь={Area:F1} га",
                simulationId, activeSimulation.CurrentStep, stepResult.FireArea);
        }

        activeSimulation.StepResults.Add(stepResult);
        await SaveSimulationToDbAsync(activeSimulation);

        return stepResult;
    }

    private async Task SendEventsToKafka(SimulationStepResult stepResult, ActiveSimulation activeSimulation)
    {
        _logger.LogInformation("📤 SendEventsToKafka: отправка метрик для шага {Step}, площадь={Area:F1} га",
            stepResult.Step, stepResult.FireArea);

        await _kafkaFacade.SendMetrics(
            stepResult.SimulationId,
            stepResult.Step,
            stepResult.BurningCellsCount,
            stepResult.BurnedCellsCount,
            stepResult.FireArea,
            stepResult.SpreadSpeed);

        _logger.LogInformation("✅ Метрики отправлены в Kafka для шага {Step}", stepResult.Step);

        int eventCount = 0;
        foreach (var fireEvent in stepResult.Events.OfType<CellIgnitedEvent>())
        {
            var cell = activeSimulation.Graph.Cells.FirstOrDefault(c => c.Id == fireEvent.CellId);
            if (cell != null)
            {
                await _kafkaFacade.SendFireEvent(
                    stepResult.SimulationId,
                    stepResult.Step,
                    cell.Id,
                    cell.X,
                    cell.Y,
                    cell.Vegetation.ToString(),
                    cell.State.ToString(),
                    fireEvent.IgnitionProbability);
                eventCount++;
            }
        }

        _logger.LogDebug("События шага {Step}: отправлено {Count} событий возгорания", stepResult.Step, eventCount);
    }

    public async Task<bool> StopSimulation(Guid simulationId)
    {
        await EnsureInitializedAsync();

        if (!_activeSimulations.TryGetValue(simulationId, out var activeSimulation))
        {
            _logger.LogWarning("Симуляция {SimulationId} не найдена при остановке", simulationId);
            return false;
        }

        activeSimulation.IsRunning = false;
        activeSimulation.EndTime = DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var simulationRepo = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();

        if (activeSimulation.Simulation.Status == SimulationStatus.Running)
        {
            activeSimulation.Simulation.Cancel();
            await simulationRepo.UpdateAsync(activeSimulation.Simulation);
        }

        await SaveSimulationToDbAsync(activeSimulation);

        _logger.LogInformation("Симуляция {SimulationId} остановлена", simulationId);

        return true;
    }

    public async Task<ActiveSimulation?> GetSimulation(Guid simulationId)
    {
        await EnsureInitializedAsync();

        _activeSimulations.TryGetValue(simulationId, out var simulation);

        if (simulation == null)
        {
            _logger.LogWarning("Симуляция {SimulationId} не найдена", simulationId);
        }

        return simulation;
    }

    public async Task<List<ActiveSimulation>> GetAllSimulations()
    {
        await EnsureInitializedAsync();
        return _activeSimulations.Values.ToList();
    }

    public async Task<bool> DeleteSimulation(Guid simulationId)
    {
        await EnsureInitializedAsync();

        using var scope = _scopeFactory.CreateScope();
        var simulationRepo = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();

        var simulation = await simulationRepo.GetByIdAsync(simulationId);
        if (simulation == null)
        {
            _logger.LogWarning("Симуляция {SimulationId} не найдена для удаления", simulationId);
            return false;
        }

        if (_activeSimulations.TryGetValue(simulationId, out var activeSimulation))
        {
            activeSimulation.IsRunning = false;
            activeSimulation.EndTime = DateTime.UtcNow;
            _activeSimulations.Remove(simulationId);
        }

        await DeleteSimulationFromDbAsync(simulationId);
        await simulationRepo.DeleteAsync(simulation);

        _logger.LogInformation("Симуляция {SimulationId} полностью удалена", simulationId);
        return true;
    }

    public async Task<bool> ResetSimulationAsync(Guid simulationId)
    {
        await EnsureInitializedAsync();

        _logger.LogInformation("🔄 Запрос на перезапуск симуляции {SimulationId}", simulationId);

        using var scope = _scopeFactory.CreateScope();
        var simulationRepo = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();
        var activeRepo = scope.ServiceProvider.GetRequiredService<IActiveSimulationRepository>();

        var simulation = await simulationRepo.GetByIdAsync(simulationId);
        if (simulation == null)
        {
            _logger.LogError("❌ Симуляция {SimulationId} не найдена", simulationId);
            return false;
        }

        _logger.LogInformation(
            "📋 Симуляция найдена: {Name}, Status={Status}, HasGraph={HasGraph}",
            simulation.Name,
            simulation.Status,
            simulation.HasSavedGraph());

        if (!simulation.HasSavedGraph())
        {
            _logger.LogError("❌ Граф для симуляции {SimulationId} не найден", simulationId);
            return false;
        }

        var hasLiveActiveSimulation =
            _activeSimulations.TryGetValue(simulationId, out var activeSimulation) &&
            activeSimulation.IsRunning;

        if (hasLiveActiveSimulation && activeSimulation != null)
        {
            _logger.LogInformation(
                "- Симуляция {SimulationId} сейчас выполняется. Останавливаем перед перезапуском.",
                simulationId);

            activeSimulation.IsRunning = false;
            activeSimulation.EndTime = DateTime.UtcNow;
        }

        if (simulation.Status == SimulationStatus.Running)
        {
            _logger.LogInformation(
                "- Симуляция {SimulationId} была в статусе Running. Переводим в Cancelled перед сбросом.",
                simulationId);

            simulation.Cancel();
        }

        simulation.ResetForRestart();
        simulation.CachedGraph = null;

        await simulationRepo.UpdateAsync(simulation);
        _logger.LogInformation("✅ Статус симуляции сброшен на Created");

        await activeRepo.DeleteBySimulationIdAsync(simulationId);
        _logger.LogInformation("🗑 Active-record удалён");

        if (_activeSimulations.ContainsKey(simulationId))
        {
            _activeSimulations.Remove(simulationId);
            _logger.LogInformation("🗑 Симуляция удалена из памяти");
        }

        _logger.LogInformation(
            "✅ Симуляция {SimulationId} перезапущена. Сохранённые стартовые очаги и исходный граф будут использованы повторно.",
            simulationId);

        return true;
    }

    private void ResetGraphForReplay(ForestGraph graph)
    {
        foreach (var cell in graph.Cells)
        {
            var stateField = typeof(ForestCell).GetField("<State>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            stateField?.SetValue(cell, CellState.Normal);

            var ignitionField = typeof(ForestCell).GetField("<IgnitionTime>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            ignitionField?.SetValue(cell, null);

            var burnoutField = typeof(ForestCell).GetField("<BurnoutTime>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            burnoutField?.SetValue(cell, null);

            var currentFuelLoadField = typeof(ForestCell).GetField("<CurrentFuelLoad>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            currentFuelLoadField?.SetValue(cell, cell.FuelLoad);

            var fireStageField = typeof(ForestCell).GetField("<FireStage>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            fireStageField?.SetValue(cell, FireStage.Unburned);

            var fireIntensityField = typeof(ForestCell).GetField("<FireIntensity>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            fireIntensityField?.SetValue(cell, 0.0);

            cell.SetBurnProbability(0.0);
            cell.SetBurningElapsedSeconds(0.0);
            cell.ResetAccumulatedHeat();
        }
    }
    private async Task SaveStepMetricsAsync(ActiveSimulation activeSimulation, SimulationStepResult stepResult)
    {
        using var scope = _scopeFactory.CreateScope();

        var metricsRepo = scope.ServiceProvider.GetRequiredService<IFireMetricsRepository>();

        var metrics = new FireMetrics(
            activeSimulation.Simulation,
            stepResult.Step,
            stepResult.BurningCellsCount,
            stepResult.BurnedCellsCount,
            stepResult.TotalCellsAffected,
            stepResult.SpreadSpeed,
            activeSimulation.CurrentWeather.Temperature,
            activeSimulation.CurrentWeather.WindSpeedMps
        );

        await metricsRepo.AddAsync(metrics);

        _logger.LogDebug(
            "Сохранены FireMetrics для симуляции {SimulationId}, шаг {Step}",
            stepResult.SimulationId,
            stepResult.Step);
    }
    public async Task<ForestGraph> RefreshIgnitionSetupAsync(Guid simulationId)
    {
        await EnsureInitializedAsync();

        _logger.LogInformation("🔁 Обновление стартовых очагов для симуляции {SimulationId}", simulationId);

        using var scope = _scopeFactory.CreateScope();
        var simulationRepo = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();
        var graphGenerator = scope.ServiceProvider.GetRequiredService<IForestGraphGenerator>();
        var activeRepo = scope.ServiceProvider.GetRequiredService<IActiveSimulationRepository>();

        var simulation = await simulationRepo.GetByIdAsync(simulationId);
        if (simulation == null)
            throw new InvalidOperationException($"Симуляция {simulationId} не найдена");

        if (simulation.Status != SimulationStatus.Created)
            throw new InvalidOperationException("Обновить очаги можно только для симуляции в статусе Created.");

        ForestGraph graph;

        if (simulation.HasSavedGraph())
        {
            graph = simulation.LoadGraph()
                ?? throw new InvalidOperationException($"Не удалось загрузить граф для симуляции {simulationId}");
        }
        else
        {
            graph = await CreateBaseGraphAsync(simulation, graphGenerator);
        }

        graph.StepDurationSeconds = simulation.Parameters.StepDurationSeconds > 0
            ? simulation.Parameters.StepDurationSeconds
            : 60;

        ResetGraphForReplay(graph);

        simulation.ClearInitialFirePositions();
        simulation.SaveGraph(graph);
        simulation.CachedGraph = graph;

        await simulationRepo.UpdateAsync(simulation);
        await activeRepo.DeleteBySimulationIdAsync(simulationId);

        if (_activeSimulations.ContainsKey(simulationId))
            _activeSimulations.Remove(simulationId);

        _logger.LogInformation(
            "✅ Стартовые очаги для симуляции {SimulationId} очищены, карта снова чистая",
            simulationId);

        return graph;
    }
}

public class ActiveSimulation
{
    [JsonIgnore]
    public Simulation Simulation { get; set; } = null!;

    public ForestGraph Graph { get; set; } = null!;
    public WeatherCondition CurrentWeather { get; set; } = null!;
    public int CurrentStep { get; set; }
    public bool IsRunning { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalBurnedCells { get; set; }
    public int TotalBurningCells { get; set; }

    [JsonIgnore]
    public List<SimulationStepResult> StepResults { get; set; } = new();

    [JsonIgnore]
    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;

    public double FireArea => (TotalBurnedCells + TotalBurningCells) * 1.0;

    [JsonIgnore]
    public double AverageSpreadSpeed => StepResults.Any()
        ? StepResults.Average(r => r.SpreadSpeed)
        : 0;
}