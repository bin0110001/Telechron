using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class RepairAttemptMapper
{
    public static RepairAttempt ToDomain(this RepairAttemptEntity entity) => new()
    {
        Id = entity.Id,
        FindingIds = entity.FindingLinks.Select(l => l.FindingId).ToList(),
        SnapshotRef = entity.SnapshotRef,
        PatchDiff = entity.PatchDiff,
        VerifyResultJson = entity.VerifyResultJson,
        ApprovalDecision = entity.ApprovalDecision.HasValue ? (RepairApprovalDecision)entity.ApprovalDecision.Value : null,
        ApproverUserId = entity.ApproverUserId,
        ResultingArtifactId = entity.ResultingArtifactId,
        CommitReference = entity.CommitReference,
        ProvenanceRecordJson = entity.ProvenanceRecordJson,
        CreatedAtUtc = entity.CreatedAtUtc,
    };

    // FindingLinks is deliberately NOT populated here — the repository owns
    // creating join rows, since it needs the RepairAttemptId which doesn't
    // exist as a meaningful FK value until the parent row is tracked.
    public static RepairAttemptEntity ToEntity(this RepairAttempt domain) => new()
    {
        Id = domain.Id,
        SnapshotRef = domain.SnapshotRef,
        PatchDiff = domain.PatchDiff,
        VerifyResultJson = domain.VerifyResultJson,
        ApprovalDecision = (int?)domain.ApprovalDecision,
        ApproverUserId = domain.ApproverUserId,
        ResultingArtifactId = domain.ResultingArtifactId,
        CommitReference = domain.CommitReference,
        ProvenanceRecordJson = domain.ProvenanceRecordJson,
        CreatedAtUtc = domain.CreatedAtUtc,
    };

    public static void ApplyTo(this RepairAttempt domain, RepairAttemptEntity entity)
    {
        entity.VerifyResultJson = domain.VerifyResultJson;
        entity.ApprovalDecision = (int?)domain.ApprovalDecision;
        entity.ApproverUserId = domain.ApproverUserId;
        entity.ResultingArtifactId = domain.ResultingArtifactId;
        entity.CommitReference = domain.CommitReference;
        entity.ProvenanceRecordJson = domain.ProvenanceRecordJson;
    }
}
