using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class FindingRepository(TelechronDbContext db) : IFindingRepository
{
    public async Task<Finding?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Findings.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Finding>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await db.Findings.AsNoTracking().Where(f => f.ProjectId == projectId).Select(f => f.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<Finding>> GetByRunAsync(Guid runId, CancellationToken ct = default) =>
        await db.Findings.AsNoTracking().Where(f => f.RunId == runId).Select(f => f.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<Finding>> GetAllAsync(CancellationToken ct = default) =>
        await db.Findings.AsNoTracking().Select(f => f.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Finding entity, CancellationToken ct = default)
    {
        await db.Findings.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Finding entity, CancellationToken ct = default)
    {
        var existing = await db.Findings.FirstOrDefaultAsync(f => f.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Finding {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Findings.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (existing is null) return;
        db.Findings.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
