using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class ResourceRepository(TelechronDbContext db) : IResourceRepository
{
    public async Task<Resource?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Resources.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Resource>> GetByMachineAsync(Guid machineId, CancellationToken ct = default) =>
        await db.Resources.AsNoTracking().Where(r => r.MachineId == machineId).Select(r => r.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<Resource>> GetByExclusiveGroupAsync(string exclusiveGroup, CancellationToken ct = default) =>
        await db.Resources.AsNoTracking().Where(r => r.ExclusiveGroup == exclusiveGroup).Select(r => r.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<Resource>> GetAllAsync(CancellationToken ct = default) =>
        await db.Resources.AsNoTracking().Select(r => r.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Resource entity, CancellationToken ct = default)
    {
        await db.Resources.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Resource entity, CancellationToken ct = default)
    {
        var existing = await db.Resources.FirstOrDefaultAsync(r => r.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Resource {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Resources.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (existing is null) return;
        db.Resources.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
