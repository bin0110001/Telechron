namespace Telechron.Sdk.Domain;

// R-DM5/R-WF4: DefinitionSnapshotJson is captured once at creation and never
// updated afterward — that immutability IS the Definition Pinning guarantee
// ("edits to the live Workflow definition never affect in-flight Runs; a
// paused Run always resumes against its captured snapshot").
public sealed record WorkflowRun
{
    public required Guid Id { get; init; }
    public required Guid WorkflowId { get; init; }
    public required WorkflowRunStatus Status { get; init; }
    public required string DefinitionSnapshotJson { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
}
