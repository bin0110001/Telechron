namespace Telechron.Sdk.Workflows.Approvals;

public interface IApprovalManager
{
    Task<WorkflowApprovalRequest> CreateRequestAsync(
        Guid workflowRunId, string stepId, string gateId, string prompt, CancellationToken ct = default);

    Task<WorkflowApprovalRequest?> GetRequestByIdAsync(Guid requestId, CancellationToken ct = default);

    Task<IReadOnlyList<WorkflowApprovalRequest>> GetPendingRequestsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<WorkflowApprovalRequest>> GetRequestsForRunAsync(Guid workflowRunId, CancellationToken ct = default);

    Task<WorkflowApprovalRequest> SubmitDecisionAsync(
        Guid requestId, Guid approvedByUserId, bool approve, string? comment = null, string? parameterOverridesJson = null, CancellationToken ct = default);
}
