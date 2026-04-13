using Microsoft.EntityFrameworkCore;
using WildfireSimulator.Domain.Models;
using WildfireSimulator.Infrastructure.Data;
using WildfireSimulator.Application.Interfaces;

namespace WildfireSimulator.Infrastructure.Repositories;

public class ActiveSimulationRepository : IActiveSimulationRepository
{
    private readonly ApplicationDbContext _context;

    public ActiveSimulationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ActiveSimulationRecord?> GetBySimulationIdAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        return await _context.ActiveSimulationRecords
            .Include(r => r.Simulation)
            .FirstOrDefaultAsync(r => r.SimulationId == simulationId, cancellationToken);
    }

    public async Task<IEnumerable<ActiveSimulationRecord>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ActiveSimulationRecords
            .Include(r => r.Simulation)
            .Where(r => r.IsRunning)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ActiveSimulationRecord record, CancellationToken cancellationToken = default)
    {
        await _context.ActiveSimulationRecords.AddAsync(record, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ActiveSimulationRecord record, CancellationToken cancellationToken = default)
    {
        _context.ActiveSimulationRecords.Update(record);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteBySimulationIdAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        var record = await GetBySimulationIdAsync(simulationId, cancellationToken);
        if (record != null)
        {
            _context.ActiveSimulationRecords.Remove(record);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        return await _context.ActiveSimulationRecords
            .AnyAsync(r => r.SimulationId == simulationId, cancellationToken);
    }
}
