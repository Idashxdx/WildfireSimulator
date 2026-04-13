using WildfireSimulator.Application.Models.Events;
using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Interfaces;

public interface IFireSpreadSimulator
{
    Task<SimulationStepResult> SimulateStepAsync(
    ForestGraph graph,
    WeatherCondition weather,
    int currentStep,
    Guid simulationId,
    double stepDurationSeconds);

    Task<ForestGraph> InitializeFireAsync(
        ForestGraph graph,
        int initialFireCellsCount,
        WeatherCondition weather,
        List<(int X, int Y)>? fixedPositions = null);
}

public class SimulationStepResult
{
    public Guid SimulationId { get; set; }
    public int Step { get; set; }
    public DateTime Timestamp { get; set; }

    public int BurningCellsCount { get; set; }
    public int BurnedCellsCount { get; set; }
    public int NewlyIgnitedCells { get; set; }
    public int TotalCellsAffected { get; set; }

    public double FireArea { get; set; }
    public double SpreadSpeed { get; set; }

    public List<SimulationEvent> Events { get; set; } = new();
    public string? Error { get; set; }

    public bool IsSuccess => string.IsNullOrEmpty(Error);
}