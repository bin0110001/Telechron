using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class FunctionRepository(TelechronDbContext db) : IFunctionRepository
{
    public async Task<Function?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Functions.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Function>> GetByModuleAsync(Guid moduleId, CancellationToken ct = default) =>
        await db.Functions.AsNoTracking().Where(f => f.ModuleId == moduleId).Select(f => f.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<Function>> GetAllAsync(CancellationToken ct = default) =>
        await db.Functions.AsNoTracking().Select(f => f.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Function entity, CancellationToken ct = default)
    {
        await db.Functions.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Function entity, CancellationToken ct = default)
    {
        var existing = await db.Functions.FirstOrDefaultAsync(f => f.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Function {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Functions.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (existing is null) return;
        db.Functions.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
