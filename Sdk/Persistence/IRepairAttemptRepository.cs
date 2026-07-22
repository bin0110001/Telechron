using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IRepairAttemptRepository : IRepository<RepairAttempt, Guid>
{
    // Queries through the Finding<->RepairAttempt join — R-DM3a's whole point
    // is "a Finding may accrue several attempts" being queryable.
    Task<IReadOnlyList<RepairAttempt>> GetByFindingAsync(Guid findingId, CancellationToken ct = default);
}
