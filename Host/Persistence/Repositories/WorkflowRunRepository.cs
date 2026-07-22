using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class WorkflowRunRepository(TelechronDbContext db) : IWorkflowRunRepository
{
    public async Task<WorkflowRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.WorkflowRuns.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<WorkflowRun>> GetByWorkflowAsync(Guid workflowId, CancellationToken ct = default) =>
        await db.WorkflowRuns.AsNoTracking().Where(w => w.WorkflowId == workflowId).Select(w => w.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<WorkflowRun>> GetActiveAsync(CancellationToken ct = default) =>
        await db.WorkflowRuns.AsNoTracking()
            .Where(w => w.Status == (int)WorkflowRunStatus.Pending
                || w.Status == (int)WorkflowRunStatus.Running
                || w.Status == (int)WorkflowRunStatus.AwaitingApproval)
            .Select(w => w.ToDomain())
            .ToListAsync(ct);

    public async Task<IReadOnlyList<WorkflowRun>> GetAllAsync(CancellationToken ct = default) =>
        await db.WorkflowRuns.AsNoTracking().Select(w => w.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(WorkflowRun entity, CancellationToken ct = default)
    {
        await db.WorkflowRuns.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WorkflowRun entity, CancellationToken ct = default)
    {
        var existing = await db.WorkflowRuns.FirstOrDefaultAsync(w => w.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"WorkflowRun {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.WorkflowRuns.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (existing is null) return;
        db.WorkflowRuns.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
