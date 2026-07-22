namespace Telechron.Sdk.Domain;

// An ordered graph of Function executions (R-DM5). DefinitionJson is the
// opaque graph/steps/variables/typed-artifacts/approval-gates definition —
// this phase only persists it; a later phase parses/executes it. WorkflowRun
// captures an immutable snapshot of this at start time (R-DM5 Definition
// Pinning), so edits here never affect in-flight Runs.
public sealed record Workflow
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public required string DefinitionJson { get; init; }
    public required WorkflowFailurePolicy FailurePolicy { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
