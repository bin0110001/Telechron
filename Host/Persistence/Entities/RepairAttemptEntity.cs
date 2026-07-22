namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for RepairAttempt (R-DM3a). FindingIds live in the
// separate RepairAttemptFindingEntity join table, not here.
public sealed class RepairAttemptEntity
{
    public Guid Id { get; set; }
    public string SnapshotRef { get; set; } = string.Empty;
    public string PatchDiff { get; set; } = string.Empty;
    public string? VerifyResultJson { get; set; }
    public int? ApprovalDecision { get; set; }
    public Guid? ApproverUserId { get; set; }
    public Guid? ResultingArtifactId { get; set; }
    public string? CommitReference { get; set; }
    public string? ProvenanceRecordJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public UserEntity? Approver { get; set; }
    public ArtifactEntity? ResultingArtifact { get; set; }
    public List<RepairAttemptFindingEntity> FindingLinks { get; set; } = [];
}
