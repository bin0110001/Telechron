using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Entities;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class RepairAttemptRepository(TelechronDbContext db) : IRepairAttemptRepository
{
    public async Task<RepairAttempt?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.RepairAttempts.AsNoTracking()
            .Include(r => r.FindingLinks)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<RepairAttempt>> GetByFindingAsync(Guid findingId, CancellationToken ct = default)
    {
        var entities = await db.RepairAttempts.AsNoTracking()
            .Include(r => r.FindingLinks)
            .Where(r => r.FindingLinks.Any(l => l.FindingId == findingId))
            .ToListAsync(ct);
        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<RepairAttempt>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await db.RepairAttempts.AsNoTracking().Include(r => r.FindingLinks).ToListAsync(ct);
        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task AddAsync(RepairAttempt entity, CancellationToken ct = default)
    {
        var row = entity.ToEntity();
        row.FindingLinks = entity.FindingIds
            .Select(findingId => new RepairAttemptFindingEntity { RepairAttemptId = row.Id, FindingId = findingId })
            .ToList();

        await db.RepairAttempts.AddAsync(row, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(RepairAttempt entity, CancellationToken ct = default)
    {
        var existing = await db.RepairAttempts.FirstOrDefaultAsync(r => r.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"RepairAttempt {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.RepairAttempts.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (existing is null) return;
        db.RepairAttempts.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
