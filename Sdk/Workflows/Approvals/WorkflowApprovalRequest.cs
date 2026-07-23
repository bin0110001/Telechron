namespace Telechron.Sdk.Workflows.Approvals;

public sealed record WorkflowApprovalRequest
{
    public required Guid Id { get; init; }
    public required Guid WorkflowRunId { get; init; }
    public required string StepId { get; init; }
    public required string GateId { get; init; }
    public required string Prompt { get; init; }
    public required bool IsSatisfied { get; init; }
    public Guid? ApprovedByUserId { get; init; }
    public string? ApproverComment { get; init; }
    public string? ParameterOverridesJson { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? DecisionAtUtc { get; init; }
}
