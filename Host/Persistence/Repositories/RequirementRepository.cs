using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class RequirementRepository(TelechronDbContext db) : IRequirementRepository
{
    public async Task<Requirement?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Requirements.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Requirement>> GetByDesignDocumentAsync(Guid designDocumentId, CancellationToken ct = default) =>
        await db.Requirements.AsNoTracking()
            .Where(r => r.DesignDocumentId == designDocumentId)
            .Select(r => r.ToDomain())
            .ToListAsync(ct);

    public async Task<Requirement?> GetByRequirementIdAsync(Guid designDocumentId, string requirementId, CancellationToken ct = default)
    {
        var entity = await db.Requirements.AsNoTracking()
            .FirstOrDefaultAsync(r => r.DesignDocumentId == designDocumentId && r.RequirementId == requirementId, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<Requirement>> GetAllAsync(CancellationToken ct = default) =>
        await db.Requirements.AsNoTracking().Select(r => r.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(Requirement entity, CancellationToken ct = default)
    {
        await db.Requirements.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Requirement entity, CancellationToken ct = default)
    {
        var existing = await db.Requirements.FirstOrDefaultAsync(r => r.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"Requirement {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Requirements.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (existing is null) return;
        db.Requirements.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
