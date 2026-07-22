namespace Telechron.Sdk.Domain;

// The durable record of one pass through the repair pipeline (R-DM3a).
// FindingIds is the domain-side view of the many-to-many join (a bundled
// multi-file patch may resolve several Findings; a Finding may accrue
// several attempts) — the join table itself is a Host/Persistence-only
// concern the mapper translates to/from this flat list. This record makes
// the chain Artifact/commit → RepairAttempt → Finding(s) → originating Run
// fully queryable, the primary trust surface for FullyAutonomous mode.
public sealed record RepairAttempt
{
    public required Guid Id { get; init; }
    public required IReadOnlyList<Guid> FindingIds { get; init; }
    public required string SnapshotRef { get; init; }
    public required string PatchDiff { get; init; }
    public string? VerifyResultJson { get; init; }
    public RepairApprovalDecision? ApprovalDecision { get; init; }
    public Guid? ApproverUserId { get; init; }
    public Guid? ResultingArtifactId { get; init; }
    public string? CommitReference { get; init; }
    public string? ProvenanceRecordJson { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
