using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

// R-DM16b: append-only — this interface deliberately has no Update/Delete
// beyond what IRepository provides generically; callers should only ever
// Add new revisions, never mutate history.
public interface IRequirementRevisionRepository : IRepository<RequirementRevision, Guid>
{
    Task<IReadOnlyList<RequirementRevision>> GetByRequirementAsync(Guid requirementId, CancellationToken ct = default);
}
