using Ardalis.GuardClauses;

namespace WildfireSimulator.Domain.Models;

public class FireMetrics
{
    public Guid Id { get; private set; }
    public Guid SimulationId { get; private set; }
    public int Step { get; private set; }
    public DateTime Timestamp { get; private set; }
    public int BurningCellsCount { get; private set; }
    public int BurnedCellsCount { get; private set; }
    public int TotalCellsAffected { get; private set; }
    public double FireSpreadSpeed { get; private set; }
    public double AverageTemperature { get; private set; }
    public double AverageWindSpeed { get; private set; }
    
    public virtual Simulation Simulation { get; private set; } = null!;
    
    private FireMetrics() { }
    
    public FireMetrics(
        Simulation simulation,
        int step,
        int burningCellsCount,
        int burnedCellsCount,
        int totalCellsCount,
        double fireSpreadSpeed,
        double averageTemperature,
        double averageWindSpeed)
    {
        Id = Guid.NewGuid();
        SimulationId = Guard.Against.Default(simulation.Id, nameof(simulation.Id));
        Step = Guard.Against.Negative(step, nameof(step));
        Timestamp = DateTime.UtcNow;
        BurningCellsCount = Guard.Against.Negative(burningCellsCount, nameof(burningCellsCount));
        BurnedCellsCount = Guard.Against.Negative(burnedCellsCount, nameof(burnedCellsCount));
        TotalCellsAffected = burningCellsCount + burnedCellsCount;
        FireSpreadSpeed = Guard.Against.Negative(fireSpreadSpeed, nameof(fireSpreadSpeed));
        AverageTemperature = averageTemperature;
        AverageWindSpeed = averageWindSpeed;
    }
    
    public double GetFireArea(double cellArea)
    {
        return TotalCellsAffected * cellArea;
    }
    
    public double GetFirePerimeter(int gridWidth, int gridHeight)
    {
        return Math.Sqrt(TotalCellsAffected) * 4;
    }
}
