using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class ArtifactRepository(TelechronDbContext db) : IArtifactRepository
{
    public async Task<Artifact?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Artifacts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Artifact>> GetByWorkflowRunAsync(Guid workflowRunId, CancellationToken ct = default) =>
        await db.Artifacts.AsNoTracking().Where(a => a.WorkflowRunId == workflowRunId).Select(a => a.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<Artifact>> GetAllAsync(CancellationToken ct = default) =>
        await db.Artifacts.AsNoTracking().Select(a => a.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Artifact entity, CancellationToken ct = default)
    {
        await db.Artifacts.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Artifact entity, CancellationToken ct = default)
    {
        var existing = await db.Artifacts.FirstOrDefaultAsync(a => a.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Artifact {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Artifacts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (existing is null) return;
        db.Artifacts.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
