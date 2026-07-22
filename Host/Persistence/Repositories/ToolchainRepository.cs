using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class ToolchainRepository(TelechronDbContext db) : IToolchainRepository
{
    public async Task<Toolchain?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Toolchains.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Toolchain>> GetAllAsync(CancellationToken ct = default) =>
        await db.Toolchains.AsNoTracking().Select(t => t.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Toolchain entity, CancellationToken ct = default)
    {
        await db.Toolchains.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Toolchain entity, CancellationToken ct = default)
    {
        var existing = await db.Toolchains.FirstOrDefaultAsync(t => t.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Toolchain {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Toolchains.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (existing is null) return;
        db.Toolchains.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
