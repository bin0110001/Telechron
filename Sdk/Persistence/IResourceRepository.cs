using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IResourceRepository : IRepository<Resource, Guid>
{
    Task<IReadOnlyList<Resource>> GetByMachineAsync(Guid machineId, CancellationToken ct = default);

    // Resources sharing a non-null ExclusiveGroup that must be checked for
    // mutual-exclusion scheduling (R-SCH2) before granting one to a Run.
    Task<IReadOnlyList<Resource>> GetByExclusiveGroupAsync(string exclusiveGroup, CancellationToken ct = default);
}
