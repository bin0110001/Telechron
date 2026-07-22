using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IWorkflowRepository : IRepository<Workflow, Guid>
{
    Task<IReadOnlyList<Workflow>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
}
