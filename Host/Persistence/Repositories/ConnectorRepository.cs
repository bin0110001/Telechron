using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class ConnectorRepository(TelechronDbContext db) : IConnectorRepository
{
    public async Task<Connector?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Connectors.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Connector>> GetByProjectAsync(Guid? projectId, CancellationToken ct = default) =>
        await db.Connectors.AsNoTracking().Where(c => c.ProjectId == projectId).Select(c => c.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<Connector>> GetAllAsync(CancellationToken ct = default) =>
        await db.Connectors.AsNoTracking().Select(c => c.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Connector entity, CancellationToken ct = default)
    {
        await db.Connectors.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Connector entity, CancellationToken ct = default)
    {
        var existing = await db.Connectors.FirstOrDefaultAsync(c => c.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Connector {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Connectors.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (existing is null) return;
        db.Connectors.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
