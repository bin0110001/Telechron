using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class MachineRepository(TelechronDbContext db) : IMachineRepository
{
    public async Task<Machine?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Machines.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Machine>> GetOnlineAsync(CancellationToken ct = default) =>
        await db.Machines.AsNoTracking().Where(m => m.IsOnline).Select(m => m.ToDomain()).ToListAsync(ct);

    public async Task<Machine?> GetByFingerprintAsync(string machineFingerprint, CancellationToken ct = default)
    {
        var entity = await db.Machines.AsNoTracking().FirstOrDefaultAsync(m => m.MachineFingerprint == machineFingerprint, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Machine>> GetAllAsync(CancellationToken ct = default) =>
        await db.Machines.AsNoTracking().Select(m => m.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Machine entity, CancellationToken ct = default)
    {
        await db.Machines.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Machine entity, CancellationToken ct = default)
    {
        var existing = await db.Machines.FirstOrDefaultAsync(m => m.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Machine {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Machines.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (existing is null) return;
        db.Machines.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
