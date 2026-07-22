using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

// R-DM16b: append-only. UpdateAsync/DeleteAsync intentionally throw — a
// revision, once written, is history and is never edited or removed. This is
// enforced here, not just documented, so a future caller can't accidentally
// mutate the record R-DM16 relies on for drift/intent auditability.
public sealed class RequirementRevisionRepository(TelechronDbContext db) : IRequirementRevisionRepository
{
    public async Task<RequirementRevision?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.RequirementRevisions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<RequirementRevision>> GetByRequirementAsync(Guid requirementId, CancellationToken ct = default) =>
        await db.RequirementRevisions.AsNoTracking()
            .Where(r => r.RequirementId == requirementId)
            .OrderBy(r => r.RevisionNumber)
            .Select(r => r.ToDomain())
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RequirementRevision>> GetAllAsync(CancellationToken ct = default) =>
        await db.RequirementRevisions.AsNoTracking().Select(r => r.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(RequirementRevision entity, CancellationToken ct = default)
    {
        await db.RequirementRevisions.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(RequirementRevision entity, CancellationToken ct = default) =>
        throw new NotSupportedException("RequirementRevision is append-only (R-DM16b) — revisions are never edited.");

    public Task DeleteAsync(Guid id, CancellationToken ct = default) =>
        throw new NotSupportedException("RequirementRevision is append-only (R-DM16b) — revisions are never deleted.");
}
