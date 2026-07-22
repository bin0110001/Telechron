using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class DesignDocumentRepository(TelechronDbContext db) : IDesignDocumentRepository
{
    public async Task<DesignDocument?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.DesignDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<DesignDocument?> GetByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var entity = await db.DesignDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.ProjectId == projectId, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<DesignDocument>> GetAllAsync(CancellationToken ct = default) =>
        await db.DesignDocuments.AsNoTracking().Select(d => d.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(DesignDocument entity, CancellationToken ct = default)
    {
        await db.DesignDocuments.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(DesignDocument entity, CancellationToken ct = default)
    {
        var existing = await db.DesignDocuments.FirstOrDefaultAsync(d => d.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"DesignDocument {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.DesignDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (existing is null) return;
        db.DesignDocuments.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
