using Ardalis.GuardClauses;

namespace WildfireSimulator.Domain.Models;

public class ActiveSimulationRecord
{
    public Guid Id { get; private set; }
    public Guid SimulationId { get; private set; }
    public string SimulationName { get; private set; } = string.Empty;
    public int CurrentStep { get; private set; }
    public bool IsRunning { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public int TotalBurnedCells { get; private set; }
    public int TotalBurningCells { get; private set; }

    public string WeatherData { get; private set; } = string.Empty;
    public string StepResultsData { get; private set; } = string.Empty;

    public virtual Simulation Simulation { get; private set; } = null!;

    private ActiveSimulationRecord() { }

    public ActiveSimulationRecord(
        Guid simulationId,
        string simulationName,
        int currentStep,
        bool isRunning,
        DateTime startTime,
        DateTime? endTime,
        int totalBurnedCells,
        int totalBurningCells,
        string weatherData,
        string stepResultsData)
    {
        Id = Guid.NewGuid();
        SimulationId = Guard.Against.Default(simulationId, nameof(simulationId));
        SimulationName = Guard.Against.NullOrEmpty(simulationName, nameof(simulationName));
        CurrentStep = Guard.Against.Negative(currentStep, nameof(currentStep));
        IsRunning = isRunning;
        StartTime = startTime;
        EndTime = endTime;
        TotalBurnedCells = Guard.Against.Negative(totalBurnedCells, nameof(totalBurnedCells));
        TotalBurningCells = Guard.Against.Negative(totalBurningCells, nameof(totalBurningCells));
        WeatherData = Guard.Against.NullOrEmpty(weatherData, nameof(weatherData));
        StepResultsData = Guard.Against.NullOrEmpty(stepResultsData, nameof(stepResultsData));
    }

    public void Update(
        int currentStep,
        bool isRunning,
        DateTime? endTime,
        int totalBurnedCells,
        int totalBurningCells,
        string weatherData,
        string stepResultsData)
    {
        CurrentStep = Guard.Against.Negative(currentStep, nameof(currentStep));
        IsRunning = isRunning;
        EndTime = endTime;
        TotalBurnedCells = Guard.Against.Negative(totalBurnedCells, nameof(totalBurnedCells));
        TotalBurningCells = Guard.Against.Negative(totalBurningCells, nameof(totalBurningCells));
        WeatherData = Guard.Against.NullOrEmpty(weatherData, nameof(weatherData));
        StepResultsData = Guard.Against.NullOrEmpty(stepResultsData, nameof(stepResultsData));
    }

    public void Stop(DateTime endTime)
    {
        IsRunning = false;
        EndTime = endTime;
    }
}