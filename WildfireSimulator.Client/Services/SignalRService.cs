using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using WildfireSimulator.Client.Models;

namespace WildfireSimulator.Client.Services;

public class SignalRService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly string _hubUrl;
    private bool _isConnected = false;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public event EventHandler<MovingAverageData>? OnMovingAveragesReceived;
    public event EventHandler<TrendData>? OnTrendReceived;
    public event EventHandler<AnomalyData>? OnAnomalyReceived;
    public event EventHandler<string>? OnConnected;
    public event EventHandler<string>? OnDisconnected;
    public event EventHandler<string>? OnError;
    public event EventHandler<ForecastData>? OnForecastReceived;

    public bool IsConnected => _isConnected;

    public SignalRService(string hubUrl = "http://localhost:5198/fireHub")
    {
        _hubUrl = hubUrl;
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            if (_hubConnection != null && _isConnected)
                return true;

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_hubUrl)
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)
                })
                .Build();

            RegisterHandlers();

            _hubConnection.Closed += async (error) =>
            {
                _isConnected = false;
                OnDisconnected?.Invoke(this, error?.Message ?? "Connection closed");
                await Task.CompletedTask;
            };

            _hubConnection.Reconnecting += (error) =>
            {
                _isConnected = false;
                OnDisconnected?.Invoke(this, error?.Message ?? "Reconnecting");
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += (connectionId) =>
            {
                _isConnected = true;
                OnConnected?.Invoke(this, connectionId ?? "reconnected");
                return Task.CompletedTask;
            };

            await _hubConnection.StartAsync();
            _isConnected = true;

            OnConnected?.Invoke(this, _hubConnection.ConnectionId ?? "connected");
            return true;
        }
        catch (Exception ex)
        {
            _isConnected = false;
            OnError?.Invoke(this, ex.Message);
            return false;
        }
    }

    public async Task<bool> SubscribeToSimulationAsync(string simulationId)
    {
        if (_hubConnection == null || !_isConnected)
            return false;

        try
        {
            try
            {
                await _hubConnection.SendAsync("SubscribeToSimulation", simulationId);
                return true;
            }
            catch
            {
                await _hubConnection.InvokeAsync("JoinSimulation", simulationId);
                return true;
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, ex.Message);
            return false;
        }
    }

    public async Task<bool> UnsubscribeFromSimulationAsync(string simulationId)
    {
        if (_hubConnection == null || !_isConnected)
            return false;

        try
        {
            try
            {
                await _hubConnection.SendAsync("UnsubscribeFromSimulation", simulationId);
                return true;
            }
            catch
            {
                await _hubConnection.InvokeAsync("LeaveSimulation", simulationId);
                return true;
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, ex.Message);
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync();
            }
            catch
            {
            }

            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }

        _isConnected = false;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    private void RegisterHandlers()
    {
        _hubConnection!.On<object>("MovingAveragesUpdated", data =>
        {
            try
            {
                var parsed = ExtractPayload<MovingAverageData>(data);
                if (parsed != null)
                    OnMovingAveragesReceived?.Invoke(this, parsed);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, $"MovingAveragesUpdated: {ex.Message}");
            }
        });

        _hubConnection!.On<object>("TrendUpdated", data =>
        {
            try
            {
                var parsed = ExtractPayload<TrendData>(data);
                if (parsed != null)
                    OnTrendReceived?.Invoke(this, parsed);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, $"TrendUpdated: {ex.Message}");
            }
        });

        _hubConnection!.On<object>("AnomalyDetected", data =>
        {
            try
            {
                var parsed = ExtractPayload<AnomalyData>(data);
                if (parsed != null)
                    OnAnomalyReceived?.Invoke(this, parsed);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, $"AnomalyDetected: {ex.Message}");
            }
        });
        
        _hubConnection!.On<object>("ForecastUpdated", data =>
{
    try
    {
        var parsed = ExtractPayload<ForecastData>(data);
        if (parsed != null)
            OnForecastReceived?.Invoke(this, parsed);
    }
    catch (Exception ex)
    {
        OnError?.Invoke(this, $"ForecastUpdated: {ex.Message}");
    }
});
    }

    private T? ExtractPayload<T>(object data)
    {
        if (data is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Object &&
                jsonElement.TryGetProperty("data", out var dataElement))
            {
                return JsonSerializer.Deserialize<T>(dataElement.GetRawText(), _jsonOptions);
            }

            return JsonSerializer.Deserialize<T>(jsonElement.GetRawText(), _jsonOptions);
        }

        var json = JsonSerializer.Serialize(data, _jsonOptions);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out var nestedData))
        {
            return JsonSerializer.Deserialize<T>(nestedData.GetRawText(), _jsonOptions);
        }

        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }
}