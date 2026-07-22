using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class IntentPlanRepository(TelechronDbContext db) : IIntentPlanRepository
{
    public async Task<IntentPlan?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.IntentPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<IntentPlan>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await db.IntentPlans.AsNoTracking().Where(p => p.ProjectId == projectId).Select(p => p.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<IntentPlan>> GetAllAsync(CancellationToken ct = default) =>
        await db.IntentPlans.AsNoTracking().Select(p => p.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(IntentPlan entity, CancellationToken ct = default)
    {
        await db.IntentPlans.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(IntentPlan entity, CancellationToken ct = default)
    {
        var existing = await db.IntentPlans.FirstOrDefaultAsync(p => p.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"IntentPlan {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.IntentPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (existing is null) return;
        db.IntentPlans.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
