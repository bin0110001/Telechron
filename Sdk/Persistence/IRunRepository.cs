using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IRunRepository : IRepository<Run, Guid>
{
    Task<IReadOnlyList<Run>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);

    // Runs with Status Pending or Running — the stalled-run watchdog's
    // (R-REL1, Host/Agents/Watchdog/StalledRunWatchdogPass) input set.
    Task<IReadOnlyList<Run>> GetActiveAsync(CancellationToken ct = default);
}
