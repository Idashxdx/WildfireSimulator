using Microsoft.EntityFrameworkCore;
using WildfireSimulator.Application.Interfaces;
using WildfireSimulator.Domain.Models;
using WildfireSimulator.Infrastructure.Data;

namespace WildfireSimulator.Infrastructure.Repositories;

public class FireMetricsRepository : Repository<FireMetrics>, IFireMetricsRepository
{
    public FireMetricsRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<FireMetrics>> GetBySimulationIdAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        return await _context.FireMetrics
            .Where(m => m.SimulationId == simulationId)
            .OrderBy(m => m.Step)
            .ToListAsync(cancellationToken);
    }
}