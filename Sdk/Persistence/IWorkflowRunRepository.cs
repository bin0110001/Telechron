using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IWorkflowRunRepository : IRepository<WorkflowRun, Guid>
{
    Task<IReadOnlyList<WorkflowRun>> GetByWorkflowAsync(Guid workflowId, CancellationToken ct = default);

    // Status in Pending, Running, or AwaitingApproval.
    Task<IReadOnlyList<WorkflowRun>> GetActiveAsync(CancellationToken ct = default);
}
