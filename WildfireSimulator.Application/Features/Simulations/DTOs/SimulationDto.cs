using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Features.Simulations.DTOs;

public class SimulationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public SimulationStatus Status { get; set; }
    public GraphType GraphType { get; set; }
    public GraphScaleType? GraphScaleType { get; set; }

    public static SimulationDto FromEntity(Simulation simulation)
    {
        return new SimulationDto
        {
            Id = simulation.Id,
            Name = simulation.Name,
            Description = simulation.Description,
            CreatedAt = simulation.CreatedAt,
            StartedAt = simulation.StartedAt,
            FinishedAt = simulation.FinishedAt,
            Status = simulation.Status,
            GraphType = simulation.Parameters.GraphType,
            GraphScaleType = simulation.Parameters.GraphScaleType
        };
    }
}