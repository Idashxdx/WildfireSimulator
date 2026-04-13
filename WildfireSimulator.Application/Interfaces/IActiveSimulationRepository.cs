using WildfireSimulator.Domain.Models;

namespace WildfireSimulator.Application.Interfaces;

public interface IActiveSimulationRepository
{
    Task<ActiveSimulationRecord?> GetBySimulationIdAsync(Guid simulationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ActiveSimulationRecord>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ActiveSimulationRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(ActiveSimulationRecord record, CancellationToken cancellationToken = default);
    Task DeleteBySimulationIdAsync(Guid simulationId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid simulationId, CancellationToken cancellationToken = default);
}
