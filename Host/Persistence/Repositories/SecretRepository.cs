using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class SecretRepository(TelechronDbContext db) : ISecretRepository
{
    public async Task<Secret?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Secrets.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<Secret?> GetByHandleAsync(string handle, CancellationToken ct = default)
    {
        var entity = await db.Secrets.AsNoTracking().FirstOrDefaultAsync(s => s.Handle == handle, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Secret>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await db.Secrets.AsNoTracking().Where(s => s.ProjectId == projectId).Select(s => s.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<Secret>> GetAllAsync(CancellationToken ct = default) =>
        await db.Secrets.AsNoTracking().Select(s => s.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Secret entity, CancellationToken ct = default)
    {
        await db.Secrets.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Secret entity, CancellationToken ct = default)
    {
        var existing = await db.Secrets.FirstOrDefaultAsync(s => s.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Secret {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Secrets.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (existing is null) return;
        db.Secrets.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
