namespace Telechron.Sdk.Domain;

// Any discovered issue — test failure, security vulnerability, dependency
// issue, policy violation, code quality problem, configuration fault (R-DM3).
// The unified input to the repair pipeline. WorkflowRunId is intentionally a
// plain Guid (no FK) since it's optional provenance, same reasoning as
// RunId being the "real" provenance link when one exists. FailureClass
// (R-FIX8) gates repair-candidate promotion — Environment findings never
// enter the pipeline. Fixability/FixStatus are Repair Context; the back-refs
// to RepairAttempts described in R-DM3a live on RepairAttempt's join table.
public sealed record Finding
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public Guid? RunId { get; init; }
    public Guid? WorkflowRunId { get; init; }
    public string? OriginFilePath { get; init; }
    public required string RootCauseSignature { get; init; }
    public required FindingSeverity Severity { get; init; }
    public required string Category { get; init; }
    public required FindingFailureClass FailureClass { get; init; }
    public string? Fixability { get; init; }
    public string? FixStatus { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
