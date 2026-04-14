using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Features.Simulations.DTOs;

public class FireMetricsHistoryDto
{
    public Guid Id { get; set; }
    public Guid SimulationId { get; set; }
    public int Step { get; set; }
    public DateTime Timestamp { get; set; }
    public int BurningCellsCount { get; set; }
    public int BurnedCellsCount { get; set; }
    public int TotalCellsAffected { get; set; }
    public double FireSpreadSpeed { get; set; }
    public double AverageTemperature { get; set; }
    public double AverageWindSpeed { get; set; }
    public double FireArea { get; set; }

    public static FireMetricsHistoryDto FromEntity(FireMetrics metrics, double cellArea = 1.0)
    {
        return new FireMetricsHistoryDto
        {
            Id = metrics.Id,
            SimulationId = metrics.SimulationId,
            Step = metrics.Step,
            Timestamp = metrics.Timestamp,
            BurningCellsCount = metrics.BurningCellsCount,
            BurnedCellsCount = metrics.BurnedCellsCount,
            TotalCellsAffected = metrics.TotalCellsAffected,
            FireSpreadSpeed = metrics.FireSpreadSpeed,
            AverageTemperature = metrics.AverageTemperature,
            AverageWindSpeed = metrics.AverageWindSpeed,
            FireArea = metrics.GetFireArea(cellArea)
        };
    }
}