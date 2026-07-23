using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class LlmCallRepository(TelechronDbContext db) : ILlmCallRepository
{
    public async Task<LlmCall?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.LlmCalls.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<LlmCall>> GetAllAsync(CancellationToken ct = default) =>
        await db.LlmCalls.AsNoTracking().Select(c => c.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<LlmCall>> GetSinceAsync(DateTimeOffset sinceUtc, Guid? projectId = null, CancellationToken ct = default)
    {
        // SQLite EF Core provider can't translate DateTimeOffset relational
        // comparisons -- same limitation RetentionPass hit in Phase 3.
        // Filtering client-side is fine at this scale (a rolling spend-cap
        // window, not the whole table).
        var all = await db.LlmCalls.AsNoTracking().Select(c => c.ToDomain()).ToListAsync(ct);
        return all
            .Where(c => c.OccurredAtUtc >= sinceUtc && (projectId is null || c.ProjectId == projectId))
            .ToList();
    }

    public async Task AddAsync(LlmCall entity, CancellationToken ct = default)
    {
        await db.LlmCalls.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LlmCall entity, CancellationToken ct = default)
    {
        var existing = await db.LlmCalls.FirstOrDefaultAsync(c => c.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"LlmCall {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.LlmCalls.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (existing is null) return;
        db.LlmCalls.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
