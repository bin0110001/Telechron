using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class LlmConnectionRepository(TelechronDbContext db) : ILlmConnectionRepository
{
    public async Task<LlmConnection?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.LlmConnections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<LlmConnection>> GetAllAsync(CancellationToken ct = default) =>
        await db.LlmConnections.AsNoTracking().Select(c => c.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(LlmConnection entity, CancellationToken ct = default)
    {
        await db.LlmConnections.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LlmConnection entity, CancellationToken ct = default)
    {
        var existing = await db.LlmConnections.FirstOrDefaultAsync(c => c.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"LlmConnection {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.LlmConnections.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (existing is null) return;
        db.LlmConnections.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
