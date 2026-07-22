using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class WorkflowRepository(TelechronDbContext db) : IWorkflowRepository
{
    public async Task<Workflow?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Workflows.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Workflow>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await db.Workflows.AsNoTracking().Where(w => w.ProjectId == projectId).Select(w => w.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<Workflow>> GetAllAsync(CancellationToken ct = default) =>
        await db.Workflows.AsNoTracking().Select(w => w.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Workflow entity, CancellationToken ct = default)
    {
        await db.Workflows.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Workflow entity, CancellationToken ct = default)
    {
        var existing = await db.Workflows.FirstOrDefaultAsync(w => w.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Workflow {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Workflows.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (existing is null) return;
        db.Workflows.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
