using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class ProjectRepository(TelechronDbContext db) : IProjectRepository
{
    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Project>> GetByOwnerAsync(Guid ownerUserId, CancellationToken ct = default) =>
        await db.Projects.AsNoTracking().Where(p => p.OwnerUserId == ownerUserId).Select(p => p.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default) =>
        await db.Projects.AsNoTracking().Select(p => p.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Project entity, CancellationToken ct = default)
    {
        await db.Projects.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Project entity, CancellationToken ct = default)
    {
        var existing = await db.Projects.FirstOrDefaultAsync(p => p.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Project {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (existing is null) return;
        db.Projects.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
