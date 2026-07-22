using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IArtifactRepository : IRepository<Artifact, Guid>
{
    Task<IReadOnlyList<Artifact>> GetByWorkflowRunAsync(Guid workflowRunId, CancellationToken ct = default);
}
