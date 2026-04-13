using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Interfaces;

public interface IFireMetricsRepository : IRepository<FireMetrics>
{
    Task<IEnumerable<FireMetrics>> GetBySimulationIdAsync(Guid simulationId, CancellationToken cancellationToken = default);
}