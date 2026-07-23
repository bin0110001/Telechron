namespace Telechron.Sdk.Workflows;

using Telechron.Sdk.Domain;

public interface IWorkflowEngine
{
    Task<WorkflowRun> StartWorkflowAsync(Guid workflowId, Dictionary<string, string>? inputVariables = null, CancellationToken ct = default);
    Task<WorkflowRun> ExecuteRunAsync(Guid workflowRunId, CancellationToken ct = default);
    Task<WorkflowRun> ResumeRunAsync(Guid workflowRunId, Guid approvalRequestId, CancellationToken ct = default);
    Task<WorkflowRun> CancelRunAsync(Guid workflowRunId, string reason, CancellationToken ct = default);
}
