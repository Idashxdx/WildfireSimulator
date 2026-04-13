using Microsoft.EntityFrameworkCore;
using WildfireSimulator.Domain.Models;
using WildfireSimulator.Infrastructure.Data;
using WildfireSimulator.Application.Interfaces;

namespace WildfireSimulator.Infrastructure.Repositories;

public class SimulationRepository : Repository<Simulation>, ISimulationRepository
{
    public SimulationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Simulation>> GetByStatusAsync(SimulationStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Simulations
            .Where(s => s.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Simulation>> GetWithMetricsAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        return await _context.Simulations
            .Include(s => s.Metrics)
            .Where(s => s.Id == simulationId)
            .ToListAsync(cancellationToken);
    }
}
