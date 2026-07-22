using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class PersonaRepository(TelechronDbContext db) : IPersonaRepository
{
    public async Task<Persona?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Persona>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await db.Personas.AsNoTracking().Where(p => p.ProjectId == projectId).Select(p => p.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<Persona>> GetAllAsync(CancellationToken ct = default) =>
        await db.Personas.AsNoTracking().Select(p => p.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Persona entity, CancellationToken ct = default)
    {
        await db.Personas.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Persona entity, CancellationToken ct = default)
    {
        var existing = await db.Personas.FirstOrDefaultAsync(p => p.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Persona {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Personas.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (existing is null) return;
        db.Personas.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
