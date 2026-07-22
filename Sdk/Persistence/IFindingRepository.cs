using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IFindingRepository : IRepository<Finding, Guid>
{
    Task<IReadOnlyList<Finding>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<Finding>> GetByRunAsync(Guid runId, CancellationToken ct = default);
}
