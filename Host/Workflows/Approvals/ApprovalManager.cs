namespace Telechron.Host.Workflows.Approvals;

using System.Collections.Concurrent;
using Telechron.Sdk.Workflows.Approvals;

public sealed class ApprovalManager : IApprovalManager
{
    private readonly ConcurrentDictionary<Guid, WorkflowApprovalRequest> _requests = new();

    public Task<WorkflowApprovalRequest> CreateRequestAsync(
        Guid workflowRunId, string stepId, string gateId, string prompt, CancellationToken ct = default)
    {
        var request = new WorkflowApprovalRequest
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = workflowRunId,
            StepId = stepId,
            GateId = gateId,
            Prompt = prompt,
            IsSatisfied = false,
            ApprovedByUserId = null,
            ApproverComment = null,
            ParameterOverridesJson = null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            DecisionAtUtc = null,
        };

        _requests[request.Id] = request;
        return Task.FromResult(request);
    }

    public Task<WorkflowApprovalRequest?> GetRequestByIdAsync(Guid requestId, CancellationToken ct = default)
    {
        _requests.TryGetValue(requestId, out var request);
        return Task.FromResult(request);
    }

    public Task<IReadOnlyList<WorkflowApprovalRequest>> GetPendingRequestsAsync(CancellationToken ct = default)
    {
        var pending = _requests.Values.Where(r => !r.IsSatisfied).ToList();
        return Task.FromResult<IReadOnlyList<WorkflowApprovalRequest>>(pending);
    }

    public Task<IReadOnlyList<WorkflowApprovalRequest>> GetRequestsForRunAsync(Guid workflowRunId, CancellationToken ct = default)
    {
        var runRequests = _requests.Values.Where(r => r.WorkflowRunId == workflowRunId).ToList();
        return Task.FromResult<IReadOnlyList<WorkflowApprovalRequest>>(runRequests);
    }

    public Task<WorkflowApprovalRequest> SubmitDecisionAsync(
        Guid requestId, Guid approvedByUserId, bool approve, string? comment = null, string? parameterOverridesJson = null, CancellationToken ct = default)
    {
        if (!_requests.TryGetValue(requestId, out var existing))
        {
            throw new KeyNotFoundException($"Workflow approval request '{requestId}' was not found.");
        }

        if (existing.IsSatisfied)
        {
            throw new InvalidOperationException($"Workflow approval request '{requestId}' is already satisfied.");
        }

        var updated = existing with
        {
            IsSatisfied = approve,
            ApprovedByUserId = approvedByUserId,
            ApproverComment = comment,
            ParameterOverridesJson = parameterOverridesJson,
            DecisionAtUtc = DateTimeOffset.UtcNow
        };

        _requests[requestId] = updated;
        return Task.FromResult(updated);
    }
}
