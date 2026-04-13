using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WildfireSimulator.API.Hubs;

namespace WildfireSimulator.API.Services;

public class KafkaSignalRBridgeService : BackgroundService
{
    private readonly ILogger<KafkaSignalRBridgeService> _logger;
    private readonly string _bootstrapServers;
    private readonly IHubContext<FireHub> _hubContext;
    private IConsumer<Ignore, string>? _consumer;
    private bool _kafkaAvailable = false;

    private readonly string[] _topics = new[]
    {
        "fire-moving-averages",
        "fire-trends",
        "fire-anomalies",
        "fire-forecasts"
    };

    public KafkaSignalRBridgeService(
        ILogger<KafkaSignalRBridgeService> logger,
        IHubContext<FireHub> hubContext,
        IConfiguration configuration)
    {
        _logger = logger;
        _hubContext = hubContext;
        _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Kafka-SignalR Bridge Service запускается... BootstrapServers={BootstrapServers}",
            _bootstrapServers);

        await Task.Delay(10000, stoppingToken);
        await TryConnectToKafkaAsync(stoppingToken);

        if (!_kafkaAvailable)
        {
            _logger.LogWarning("Kafka недоступна. Bridge работает в режиме ожидания.");
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
                using var adminClient = new AdminClientBuilder(new AdminClientConfig
                {
                    BootstrapServers = _bootstrapServers,
                    SocketTimeoutMs = 5000
                }).Build();

                adminClient.GetMetadata(TimeSpan.FromSeconds(5));
                _kafkaAvailable = true;

                _logger.LogInformation("Kafka доступна для SignalR Bridge");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Попытка подключения Bridge к Kafka #{Attempt} не удалась: {Message}",
                    i + 1,
                    ex.Message);

                await Task.Delay(3000, stoppingToken);
            }
        }
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

                _logger.LogInformation("Kafka стала доступна. Запускаем Bridge...");
                _kafkaAvailable = true;
                await StartProcessing(stoppingToken);
                break;
            }
            catch
            {
            }
        }
    }

    private async Task StartProcessing(CancellationToken stoppingToken)
    {
        try
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                GroupId = $"signalr-bridge-{Guid.NewGuid()}",
                AutoOffsetReset = AutoOffsetReset.Latest,
                EnableAutoCommit = true,
                AllowAutoCreateTopics = true,
                SessionTimeoutMs = 6000,
                MaxPollIntervalMs = 300000,
                SocketTimeoutMs = 10000
            };

            _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
            _consumer.Subscribe(_topics);

            _logger.LogInformation(
                "SignalR Bridge подписан на топики: {Topics}",
                string.Join(", ", _topics));

            await ConsumeLoop(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Bridge Service остановлен по запросу");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в Bridge Service");
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
        try
        {
            while (!stoppingToken.IsCancellationRequested && _kafkaAvailable)
            {
                try
                {
                    var consumeResult = _consumer!.Consume(TimeSpan.FromMilliseconds(100));

                    if (consumeResult?.Message != null)
                    {
                        await ProcessAndForward(consumeResult.Topic, consumeResult.Message.Value);
                        _consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex) when (ex.Error.IsLocalError)
                {
                    await Task.Delay(100, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning("Ошибка consume в Bridge: {Reason}", ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в цикле потребления Bridge");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Цикл потребления Bridge остановлен");
        }
    }

    private async Task ProcessAndForward(string topic, string messageJson)
    {
        try
        {
            string cleanJson = messageJson;

            if (messageJson.TrimStart().StartsWith("\""))
            {
                using JsonDocument stringDoc = JsonDocument.Parse(messageJson);
                cleanJson = stringDoc.RootElement.GetString() ?? messageJson;
            }

            using JsonDocument dataDoc = JsonDocument.Parse(cleanJson);
            var data = dataDoc.RootElement.Clone();

            if (!data.TryGetProperty("simulationId", out var simIdElement))
            {
                _logger.LogWarning("Bridge: в сообщении из {Topic} нет simulationId", topic);
                return;
            }

            var simulationId = simIdElement.GetString();
            if (string.IsNullOrWhiteSpace(simulationId))
            {
                _logger.LogWarning("Bridge: пустой simulationId в сообщении из {Topic}", topic);
                return;
            }

            string signalREvent = topic switch
            {
                "fire-moving-averages" => "MovingAveragesUpdated",
                "fire-trends" => "TrendUpdated",
                "fire-anomalies" => "AnomalyDetected",
                "fire-forecasts" => "ForecastUpdated",
                _ => "UnknownEvent"
            };

            await _hubContext.Clients.Group($"simulation-{simulationId}").SendAsync(
                signalREvent,
                new
                {
                    topic,
                    timestamp = DateTime.UtcNow,
                    data
                },
                CancellationToken.None);

            if (topic == "fire-forecasts")
            {
                _logger.LogInformation(
                    "✅ Forecast forwarded to SignalR for simulation {SimulationId}",
                    simulationId);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Ошибка парсинга JSON в Bridge из {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка форвардинга сообщения из {Topic}", topic);
        }
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
    }

    public override void Dispose()
    {
        Cleanup();
        base.Dispose();
    }
}