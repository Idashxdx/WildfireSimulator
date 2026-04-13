using Microsoft.Extensions.Logging;
using WildfireSimulator.Application.Interfaces;
using System.Text.Json;
using Confluent.Kafka;

namespace WildfireSimulator.Application.Services;

public class KafkaFacade
{
    private readonly IKafkaProducerService _producer;
    private readonly ILogger<KafkaFacade> _logger;
    private int _sentCount = 0;

    public KafkaFacade(
        IKafkaProducerService producer,
        ILogger<KafkaFacade> logger)
    {
        _producer = producer;
        _logger = logger;
        _logger.LogInformation(" KafkaFacade инициализирован, producer type: {ProducerType}", producer.GetType().Name);
    }

    public async Task SendSimulationStart(Guid simulationId, string name, int gridWidth, int gridHeight, int initialFireCellsCount, int burningCells)
    {
        try
        {
            var fireEvent = new FireEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = "SimulationStarted",
                SimulationId = simulationId,
                Step = 0,
                Timestamp = DateTime.UtcNow,
                CellId = Guid.Empty.ToString(),
                X = 0,
                Y = 0,
                Vegetation = "None",
                State = "Started",
                IgnitionProbability = 0,
                Temperature = 25,
                Humidity = 40,
                WindSpeed = 5,
                WindDirection = "Northeast"
            };
            
            _logger.LogInformation("📤 Kafka: отправка SimulationStarted для {SimulationId}", simulationId);
            await _producer.ProduceFireEventAsync(fireEvent);
            
            await SendMetrics(simulationId, 0, burningCells, 0, burningCells * 1.0, 0);
            
            _logger.LogInformation("✅ Kafka: SimulationStart отправлен для {SimulationId}", simulationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка отправки SimulationStart в Kafka");
        }
    }

    public async Task SendFireEvent(Guid simulationId, int step, Guid cellId, int x, int y, string vegetation, string state, double ignitionProbability)
    {
        try
        {
            var fireEvent = new FireEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = "CellStateChanged",
                SimulationId = simulationId,
                Step = step,
                Timestamp = DateTime.UtcNow,
                CellId = cellId.ToString(),
                X = x,
                Y = y,
                Vegetation = vegetation,
                State = state,
                IgnitionProbability = ignitionProbability,
                Temperature = 25,
                Humidity = 40,
                WindSpeed = 5,
                WindDirection = "Northeast"
            };
            
            _logger.LogDebug("📤 Kafka: отправка FireEvent для клетки ({X},{Y}) на шаге {Step}", x, y, step);
            await _producer.ProduceFireEventAsync(fireEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка отправки FireEvent в Kafka");
        }
    }

    public async Task SendMetrics(Guid simulationId, int step, int burningCellsCount, int burnedCellsCount, double fireArea, double spreadSpeed)
    {
        try
        {
            _sentCount++;
            _logger.LogInformation("📤 KafkaFacade.SendMetrics: начало отправки для шага {Step} (#{Count})", step, _sentCount);
            
            var metricsData = new
            {
                eventId = Guid.NewGuid().ToString(),
                eventType = "MetricsUpdated",
                simulationId = simulationId.ToString(),
                step = step,
                timestamp = DateTime.UtcNow,
                burningCellsCount = burningCellsCount,
                burnedCellsCount = burnedCellsCount,
                totalCellsAffected = burningCellsCount + burnedCellsCount,
                fireArea = fireArea,
                spreadSpeed = spreadSpeed
            };
            
            var json = JsonSerializer.Serialize(metricsData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
            
            _logger.LogInformation("📤 Kafka: отправка метрик в fire-metrics для шага {Step}: {Json}", step, json);
            
            await _producer.ProduceAsync("fire-metrics", json);
            
            _logger.LogInformation("✅ Kafka: метрики для шага {Step} отправлены: burning={Burning}, burned={Burned} (#{Count})",
                step, burningCellsCount, burnedCellsCount, _sentCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка отправки метрик в Kafka для шага {Step}", step);
        }
    }

    public async Task SendSimulationEnd(Guid simulationId, int step, int totalBurnedCells, double fireArea)
    {
        try
        {
            var metricsData = new
            {
                eventId = Guid.NewGuid().ToString(),
                eventType = "SimulationEnded",
                simulationId = simulationId.ToString(),
                step = step,
                timestamp = DateTime.UtcNow,
                burningCellsCount = 0,
                burnedCellsCount = totalBurnedCells,
                totalCellsAffected = totalBurnedCells,
                fireArea = fireArea,
                spreadSpeed = 0
            };
            
            var json = JsonSerializer.Serialize(metricsData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            _logger.LogInformation("📤 Kafka: отправка SimulationEnd для {SimulationId}", simulationId);
            await _producer.ProduceAsync("fire-metrics", json);
            
            _logger.LogInformation("✅ Kafka: SimulationEnd отправлен для {SimulationId}", simulationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка отправки SimulationEnd в Kafka");
        }
    }
}
