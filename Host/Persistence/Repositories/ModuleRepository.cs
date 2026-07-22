using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class ModuleRepository(TelechronDbContext db) : IModuleRepository
{
    public async Task<Module?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Modules.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<Module?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var entity = await db.Modules.AsNoTracking().FirstOrDefaultAsync(m => m.Name == name, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Module>> GetAllAsync(CancellationToken ct = default) =>
        await db.Modules.AsNoTracking().Select(m => m.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Module entity, CancellationToken ct = default)
    {
        await db.Modules.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Module entity, CancellationToken ct = default)
    {
        var existing = await db.Modules.FirstOrDefaultAsync(m => m.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Module {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Modules.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (existing is null) return;
        db.Modules.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
