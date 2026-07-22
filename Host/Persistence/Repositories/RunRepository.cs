using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class RunRepository(TelechronDbContext db) : IRunRepository
{
    public async Task<Run?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Run>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await db.Runs.AsNoTracking().Where(r => r.ProjectId == projectId).Select(r => r.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<Run>> GetActiveAsync(CancellationToken ct = default) =>
        await db.Runs.AsNoTracking()
            .Where(r => r.Status == (int)RunStatus.Pending || r.Status == (int)RunStatus.Running)
            .Select(r => r.ToDomain())
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Run>> GetAllAsync(CancellationToken ct = default) =>
        await db.Runs.AsNoTracking().Select(r => r.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Run entity, CancellationToken ct = default)
    {
        await db.Runs.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Run entity, CancellationToken ct = default)
    {
        var existing = await db.Runs.FirstOrDefaultAsync(r => r.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Run {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Runs.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (existing is null) return;
        db.Runs.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
