namespace Telechron.Sdk.Workflows;

using Telechron.Sdk.Domain;

public sealed record WorkflowDefinition
{
    public required string Name { get; init; }
    public required WorkflowFailurePolicy FailurePolicy { get; init; }
    public IReadOnlyList<WorkflowStepDefinition> Steps { get; init; } = [];
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();
}

public sealed record WorkflowStepDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string FunctionKind { get; init; }
    public string? ModuleId { get; init; }
    public IReadOnlyList<string> DependsOnStepIds { get; init; } = [];
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> InputArtifactTypes { get; init; } = [];
    public IReadOnlyList<string> OutputArtifactTypes { get; init; } = [];
    public ApprovalGateDefinition? ApprovalGate { get; init; }
}

public sealed record ApprovalGateDefinition
{
    public required string GateId { get; init; }
    public required string Prompt { get; init; }
    public bool RequiresHumanApproval { get; init; } = true;
    public string? RequiredRole { get; init; }
}
