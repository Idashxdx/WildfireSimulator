using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace WildfireSimulator.API.Hubs;

public class FireHub : Hub
{
    private readonly ILogger<FireHub> _logger;
    private static readonly ConcurrentDictionary<string, string> _connections = new();
    private static readonly ConcurrentDictionary<string, HashSet<string>> _simulationSubscriptions = new();
    
    public FireHub(ILogger<FireHub> logger)
    {
        _logger = logger;
    }
    
    public override Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        _connections.TryAdd(connectionId, connectionId);
        _logger.LogInformation("📡 Клиент подключен: {ConnectionId}", connectionId);
        return base.OnConnectedAsync();
    }
    
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        _connections.TryRemove(connectionId, out _);
        
        foreach (var simId in _simulationSubscriptions.Keys)
        {
            if (_simulationSubscriptions.TryGetValue(simId, out var connections))
            {
                connections.Remove(connectionId);
            }
        }
        
        _logger.LogInformation("📡 Клиент отключен: {ConnectionId}", connectionId);
        return base.OnDisconnectedAsync(exception);
    }
    
    public async Task SubscribeToSimulation(string simulationId)
    {
        var connectionId = Context.ConnectionId;
        
        var subscriptions = _simulationSubscriptions.GetOrAdd(simulationId, _ => new HashSet<string>());
        lock (subscriptions)
        {
            subscriptions.Add(connectionId);
        }
        
        await Groups.AddToGroupAsync(connectionId, $"simulation-{simulationId}");
        
        await Clients.Caller.SendAsync("Subscribed", new
        {
            simulationId,
            message = $"Подписка на симуляцию {simulationId} оформлена"
        });
        
        _logger.LogInformation("📡 Клиент {ConnectionId} подписан на симуляцию {SimulationId}",
            connectionId, simulationId);
    }
    
    public async Task UnsubscribeFromSimulation(string simulationId)
    {
        var connectionId = Context.ConnectionId;
        
        if (_simulationSubscriptions.TryGetValue(simulationId, out var subscriptions))
        {
            lock (subscriptions)
            {
                subscriptions.Remove(connectionId);
            }
        }
        
        await Groups.RemoveFromGroupAsync(connectionId, $"simulation-{simulationId}");
        await Clients.Caller.SendAsync("Unsubscribed", new { simulationId });
    }
    
    public async Task BroadcastToSimulation(string simulationId, string eventType, object data)
    {
        await Clients.Group($"simulation-{simulationId}").SendAsync(eventType, data);
    }
    
    public async Task GetActiveSimulations()
    {
        var activeSims = _simulationSubscriptions.Keys.ToList();
        await Clients.Caller.SendAsync("ActiveSimulations", activeSims);
    }
}
