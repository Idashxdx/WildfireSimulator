using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Interfaces;

public interface ISimulationRepository : IRepository<Simulation>
{
    Task<IEnumerable<Simulation>> GetByStatusAsync(SimulationStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<Simulation>> GetWithMetricsAsync(Guid simulationId, CancellationToken cancellationToken = default);
}
