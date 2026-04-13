using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace WildfireSimulator.Application.Services;

public class KafkaStreamsService : BackgroundService
{
    private readonly ILogger<KafkaStreamsService> _logger;
    private readonly string _bootstrapServers;
    private readonly ConcurrentDictionary<Guid, SimulationStreamState> _streamStates = new();
    private IProducer<Null, string>? _producer;
    private IConsumer<Ignore, string>? _consumer;
    private bool _kafkaAvailable = false;
    private int _messageCount = 0;
    private readonly HashSet<string> _processedSimulations = new();

    private const int MaxHistorySize = 20;
    private const int RegressionMinPoints = 4;
    private const int RegressionMaxPoints = 6;

    private const string InputTopic = "fire-metrics";
    private const string OutputMovingAvgTopic = "fire-moving-averages";
    private const string OutputAnomaliesTopic = "fire-anomalies";
    private const string OutputTrendsTopic = "fire-trends";

    private const string OutputForecastTopic = "fire-forecasts";

    private const string ConsumerGroup = "kafka-streams-processor";

    private static readonly Counter MessagesProcessed = Metrics
        .CreateCounter("kafka_streams_messages_processed_total", "Total messages processed by Kafka Streams");

    private static readonly Counter MessagesPublished = Metrics
        .CreateCounter("kafka_streams_messages_published_total", "Total messages published by Kafka Streams",
            new CounterConfiguration { LabelNames = new[] { "topic" } });

    private static readonly Gauge CurrentLag = Metrics
        .CreateGauge("kafka_streams_current_lag", "Current consumer lag for Kafka Streams");

    private static readonly Histogram ProcessingTime = Metrics
        .CreateHistogram("kafka_streams_processing_seconds", "Message processing time in seconds");

    private static readonly Gauge ActiveSimulations = Metrics
        .CreateGauge("kafka_streams_active_simulations", "Number of active simulations in Streams state");

    public KafkaStreamsService(
        ILogger<KafkaStreamsService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "🚀 Kafka Streams Service запускается... BootstrapServers={BootstrapServers}",
            _bootstrapServers);

        await Task.Delay(5000, stoppingToken);

        await TryConnectToKafkaAsync(stoppingToken);

        if (!_kafkaAvailable)
        {
            _logger.LogWarning("⚠️ Kafka недоступна. Сервис работает в режиме ожидания.");
            _ = Task.Run(() => WaitForKafkaAndStart(stoppingToken), stoppingToken);
            return;
        }

        await StartProcessing(stoppingToken);
    }

    private async Task TryConnectToKafkaAsync(CancellationToken stoppingToken)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                _logger.LogInformation(
                    "Попытка подключения к Kafka #{Attempt}. BootstrapServers={BootstrapServers}",
                    i + 1,
                    _bootstrapServers);

                using var adminClient = new AdminClientBuilder(new AdminClientConfig
                {
                    BootstrapServers = _bootstrapServers,
                    SocketTimeoutMs = 5000
                }).Build();

                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
                _logger.LogInformation("✅ Kafka доступна. Брокеров: {Brokers}", metadata.Brokers.Count);

                _kafkaAvailable = true;
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Попытка {Attempt} не удалась: {Message}", i + 1, ex.Message);
                await Task.Delay(3000, stoppingToken);
            }
        }

        _logger.LogError("❌ Не удалось подключиться к Kafka после 5 попыток");
    }

    private async Task WaitForKafkaAndStart(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(30000, stoppingToken);

                using var adminClient = new AdminClientBuilder(new AdminClientConfig
                {
                    BootstrapServers = _bootstrapServers,
                    SocketTimeoutMs = 5000
                }).Build();

                adminClient.GetMetadata(TimeSpan.FromSeconds(5));

                _logger.LogInformation("✅ Kafka стала доступна! Запускаем обработку...");
                _kafkaAvailable = true;
                await StartProcessing(stoppingToken);
                break;
            }
            catch
            {
                _logger.LogDebug("Kafka все еще недоступна...");
            }
        }
    }

    private async Task StartProcessing(CancellationToken stoppingToken)
    {
        try
        {
            _producer = new ProducerBuilder<Null, string>(new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                Acks = Acks.All,
                SocketTimeoutMs = 10000,
                MessageTimeoutMs = 10000,
                EnableIdempotence = true
            }).Build();

            _logger.LogInformation("✅ Kafka producer создан");

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                GroupId = ConsumerGroup,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true,
                AllowAutoCreateTopics = true,
                SessionTimeoutMs = 6000,
                MaxPollIntervalMs = 300000,
                SocketTimeoutMs = 10000,
                MaxPartitionFetchBytes = 1048576
            };

            _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
            _consumer.Subscribe(InputTopic);

            _logger.LogInformation(
                "📡 Подписка на топик {Topic} с группой {Group}. BootstrapServers={BootstrapServers}",
                InputTopic,
                ConsumerGroup,
                _bootstrapServers);

            await ConsumeLoop(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Kafka Streams Service остановлен по запросу");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка в Kafka Streams Service: {Message}", ex.Message);
            _kafkaAvailable = false;
            _ = Task.Run(() => WaitForKafkaAndStart(stoppingToken), stoppingToken);
        }
        finally
        {
            Cleanup();
        }
    }

    private async Task ConsumeLoop(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 Запуск цикла потребления сообщений");

        try
        {
            while (!stoppingToken.IsCancellationRequested && _kafkaAvailable)
            {
                try
                {
                    var consumeResult = _consumer!.Consume(TimeSpan.FromMilliseconds(100));

                    if (consumeResult?.Message != null)
                    {
                        using (ProcessingTime.NewTimer())
                        {
                            _messageCount++;
                            MessagesProcessed.Inc();

                            _logger.LogInformation(
                                "📨 Получено сообщение #{MsgCount} из {Topic}, offset {Offset}",
                                _messageCount,
                                consumeResult.Topic,
                                consumeResult.Offset);

                            ProcessMetric(consumeResult.Message.Value);
                            _consumer.Commit(consumeResult);
                        }
                    }

                    ActiveSimulations.Set(_streamStates.Count);
                }
                catch (ConsumeException ex) when (ex.Error.IsLocalError)
                {
                    _logger.LogDebug("Локальная ошибка consumer: {Reason}", ex.Error.Reason);
                    await Task.Delay(100, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Ошибка при потреблении сообщения: {Reason}", ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Неожиданная ошибка в consumer");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Цикл потребления остановлен");
        }
    }

    private void ProcessMetric(string messageJson)
    {
        try
        {
            _logger.LogDebug("🔍 Начало обработки метрики, исходный JSON: {Json}", messageJson);

            string cleanJson = messageJson;

            if (messageJson.TrimStart().StartsWith("\""))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(messageJson);
                    cleanJson = doc.RootElement.GetString() ?? messageJson;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Не удалось распарсить как строку: {Message}", ex.Message);
                }
            }

            using JsonDocument dataDoc = JsonDocument.Parse(cleanJson);
            JsonElement data = dataDoc.RootElement;

            if (!data.TryGetProperty("simulationId", out var simIdElement))
            {
                _logger.LogWarning("⚠️ simulationId не найден в сообщении");
                return;
            }

            var simulationIdStr = simIdElement.GetString();
            if (string.IsNullOrEmpty(simulationIdStr))
            {
                _logger.LogWarning("⚠️ simulationId пустой");
                return;
            }

            var simulationId = Guid.Parse(simulationIdStr);

            lock (_processedSimulations)
            {
                _processedSimulations.Add(simulationIdStr);
            }

            if (!data.TryGetProperty("step", out var stepElement))
            {
                _logger.LogWarning("⚠️ step не найден для симуляции {SimId}", simulationIdStr);
                return;
            }

            if (!data.TryGetProperty("fireArea", out var areaElement))
            {
                _logger.LogWarning("⚠️ fireArea не найден для симуляции {SimId}", simulationIdStr);
                return;
            }

            var step = stepElement.GetInt32();
            var fireArea = areaElement.GetDouble();

            var state = _streamStates.GetOrAdd(simulationId, _ => new SimulationStreamState());

            bool shouldPublish = false;

            lock (state)
            {
                if (state.LastForecastForNextStep.HasValue &&
                    state.LastForecastSourceStep.HasValue &&
                    state.LastForecastSourceStep.Value == step - 1)
                {
                    var absError = Math.Abs(fireArea - state.LastForecastForNextStep.Value);
                    state.LastForecastAbsoluteError = absError;
                    state.ForecastErrorCount++;

                    if (state.ForecastErrorCount == 1)
                    {
                        state.MeanAbsoluteError = absError;
                    }
                    else
                    {
                        state.MeanAbsoluteError =
                            ((state.MeanAbsoluteError * (state.ForecastErrorCount - 1)) + absError)
                            / state.ForecastErrorCount;
                    }
                }

                state.AreaHistory.Add(new AreaPoint
                {
                    Step = step,
                    Area = fireArea,
                    Timestamp = DateTime.UtcNow
                });

                if (state.AreaHistory.Count > MaxHistorySize)
                    state.AreaHistory.RemoveAt(0);

                if (state.AreaHistory.Count >= 2)
                {
                    CalculateMetrics(state);

                    state.LastForecastForNextStep = state.ForecastNextArea;
                    state.LastForecastSourceStep = step;
                    shouldPublish = true;
                }
                else
                {
                    state.ForecastMethod = "current-value";
                    state.ForecastBasedOnPoints = 1;
                    state.ForecastDelta = 0.0;
                    state.ForecastNextArea = fireArea;
                    state.LastForecastForNextStep = fireArea;
                    state.LastForecastSourceStep = step;
                }
            }

            if (shouldPublish)
            {
                _ = Task.Run(() => PublishMetricsAsync(simulationId, state));
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ Ошибка парсинга JSON: {Message}", ex.Message);
            _logger.LogError("Проблемный JSON: {Json}", messageJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Неожиданная ошибка при обработке метрики");
        }
    }

    private void CalculateMetrics(SimulationStreamState state)
    {
        var history = state.AreaHistory.OrderBy(p => p.Step).ToList();
        var count = history.Count;

        if (count >= 3)
            state.MovingAverage3 = history.TakeLast(3).Average(p => p.Area);

        if (count >= 5)
            state.MovingAverage5 = history.TakeLast(5).Average(p => p.Area);

        if (count >= 10)
            state.MovingAverage10 = history.TakeLast(10).Average(p => p.Area);

        if (count >= 2)
        {
            state.Speed = history[^1].Area - history[^2].Area;
        }

        if (count >= 3)
        {
            var speed1 = history[^2].Area - history[^3].Area;
            var speed2 = history[^1].Area - history[^2].Area;
            state.Acceleration = speed2 - speed1;
        }

        if (count >= 4)
        {
            var last = history[^1].Area;
            var prevAvg = history.Take(count - 1).Average(p => p.Area);
            state.HasAnomaly = last > prevAvg * 2.0 || last < prevAvg * 0.5;
        }
        else
        {
            state.HasAnomaly = false;
        }

        var recentDiffs = new List<double>();
        for (int i = Math.Max(1, count - 4); i < count; i++)
        {
            recentDiffs.Add(history[i].Area - history[i - 1].Area);
        }

        var avgRecentDelta = recentDiffs.Count > 0 ? recentDiffs.Average() : state.Speed;
        var fallbackForecast = Math.Max(0.0, history[^1].Area + avgRecentDelta);

        state.ForecastDelta = avgRecentDelta;
        state.ForecastNextArea = fallbackForecast;
        state.ForecastMethod = "recent-average-delta";
        state.ForecastBasedOnPoints = Math.Min(count, 4);

        if (count >= RegressionMinPoints)
        {
            var regressionPoints = history
                .TakeLast(Math.Min(count, RegressionMaxPoints))
                .ToList();

            var regressionForecast = CalculateLinearRegressionForecast(regressionPoints);

            if (regressionForecast.HasValue)
            {
                var nextArea = Math.Max(0.0, regressionForecast.Value);
                state.ForecastDelta = nextArea - history[^1].Area;
                state.ForecastNextArea = nextArea;
                state.ForecastMethod = "linear-regression";
                state.ForecastBasedOnPoints = regressionPoints.Count;
            }
        }
    }

    private double? CalculateLinearRegressionForecast(List<AreaPoint> points)
    {
        if (points == null || points.Count < RegressionMinPoints)
            return null;

        var ordered = points.OrderBy(p => p.Step).ToList();
        double nextStep = ordered[^1].Step + 1;

        double xMean = ordered.Average(p => (double)p.Step);
        double yMean = ordered.Average(p => p.Area);

        double numerator = 0.0;
        double denominator = 0.0;

        foreach (var point in ordered)
        {
            double dx = point.Step - xMean;
            double dy = point.Area - yMean;

            numerator += dx * dy;
            denominator += dx * dx;
        }

        if (Math.Abs(denominator) < 1e-9)
            return null;

        double slope = numerator / denominator;
        double intercept = yMean - slope * xMean;

        if (double.IsNaN(slope) || double.IsInfinity(slope) ||
            double.IsNaN(intercept) || double.IsInfinity(intercept))
        {
            return null;
        }

        var predicted = intercept + slope * nextStep;

        if (double.IsNaN(predicted) || double.IsInfinity(predicted))
            return null;

        return predicted;
    }

    private async Task PublishMetricsAsync(Guid simulationId, SimulationStreamState state)
    {
        try
        {
            if (_producer == null || !_kafkaAvailable)
            {
                _logger.LogWarning("⚠️ Producer недоступен для публикации метрик");
                return;
            }

            var history = state.AreaHistory;
            if (!history.Any())
                return;

            var lastPoint = history.Last();
            var simulationIdStr = simulationId.ToString();

            var movingAvgData = new
            {
                simulationId = simulationIdStr,
                timestamp = DateTime.UtcNow,
                step = lastPoint.Step,
                currentArea = Math.Round(lastPoint.Area, 2),
                movingAverage3 = Math.Round(state.MovingAverage3, 2),
                movingAverage5 = Math.Round(state.MovingAverage5, 2),
                movingAverage10 = Math.Round(state.MovingAverage10, 2),
                speed = Math.Round(state.Speed, 2),
                acceleration = Math.Round(state.Acceleration, 2)
            };

            await PublishJsonAsync(OutputMovingAvgTopic, movingAvgData, simulationIdStr, "moving averages");

            string trend = state.Acceleration switch
            {
                > 0.5 => "ACCELERATING",
                < -0.5 => "DECELERATING",
                _ => "STABLE"
            };

            var trendData = new
            {
                simulationId = simulationIdStr,
                timestamp = DateTime.UtcNow,
                step = lastPoint.Step,
                trend = trend,
                speed = Math.Round(state.Speed, 2),
                acceleration = Math.Round(state.Acceleration, 2),
                isCritical = state.Acceleration > 1.0 || state.Speed > 2.0
            };

            await PublishJsonAsync(OutputTrendsTopic, trendData, simulationIdStr, "trend");

            var forecastData = new
            {
                simulationId = simulationIdStr,
                timestamp = DateTime.UtcNow,
                step = lastPoint.Step,
                currentArea = Math.Round(lastPoint.Area, 2),
                forecastNextArea = Math.Round(state.ForecastNextArea, 2),
                forecastDelta = Math.Round(state.ForecastDelta, 2),
                basedOnPoints = state.ForecastBasedOnPoints,
                method = state.ForecastMethod,
                lastForecastAbsoluteError = Math.Round(state.LastForecastAbsoluteError, 2),
                meanAbsoluteError = Math.Round(state.MeanAbsoluteError, 2),
                forecastErrorCount = state.ForecastErrorCount
            };

            await PublishJsonAsync(OutputForecastTopic, forecastData, simulationIdStr, "forecast");

            if (state.HasAnomaly && history.Count >= 4)
            {
                var prevAvg = history.Take(history.Count - 1).Average(p => p.Area);

                var anomalyData = new
                {
                    simulationId = simulationIdStr,
                    timestamp = DateTime.UtcNow,
                    step = lastPoint.Step,
                    currentArea = Math.Round(lastPoint.Area, 2),
                    previousAvg = Math.Round(prevAvg, 2),
                    deviation = Math.Round(lastPoint.Area / prevAvg, 2),
                    reason = lastPoint.Area > prevAvg * 2.0
                        ? "Резкий рост площади"
                        : "Резкое падение площади"
                };

                await PublishJsonAsync(OutputAnomaliesTopic, anomalyData, simulationIdStr, "anomaly");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при публикации метрик в Kafka для {SimId}", simulationId);
        }
    }

    private async Task PublishJsonAsync(string topic, object payload, string simulationId, string label)
    {
        if (_producer == null)
            return;

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var result = await _producer.ProduceAsync(topic, new Message<Null, string>
        {
            Value = json
        });

        MessagesPublished.WithLabels(topic).Inc();

        _logger.LogInformation(
            "✅ Published {Label} for simulation {SimulationId} to topic {Topic} [offset={Offset}] payload={Payload}",
            label,
            simulationId,
            topic,
            result.Offset.Value,
            json);
    }

    private void Cleanup()
    {
        try
        {
            _consumer?.Close();
            _consumer?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при закрытии consumer");
        }

        try
        {
            _producer?.Flush(TimeSpan.FromSeconds(5));
            _producer?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при закрытии producer");
        }
    }

    public override void Dispose()
    {
        _logger.LogInformation("- Kafka Streams Service Disposing");
        Cleanup();
        base.Dispose();
    }

    private class SimulationStreamState
    {
        public List<AreaPoint> AreaHistory { get; set; } = new();
        public double MovingAverage3 { get; set; }
        public double MovingAverage5 { get; set; }
        public double MovingAverage10 { get; set; }
        public double Speed { get; set; }
        public double Acceleration { get; set; }
        public bool HasAnomaly { get; set; }
        public double ForecastNextArea { get; set; }
        public double ForecastDelta { get; set; }
        public string ForecastMethod { get; set; } = "current-value";
        public int ForecastBasedOnPoints { get; set; }
        public double? LastForecastForNextStep { get; set; }
        public int? LastForecastSourceStep { get; set; }
        public double LastForecastAbsoluteError { get; set; }
        public double MeanAbsoluteError { get; set; }
        public int ForecastErrorCount { get; set; }
    }

    private class AreaPoint
    {
        public int Step { get; set; }
        public double Area { get; set; }
        public DateTime Timestamp { get; set; }
    }
}