namespace Telechron.Sdk.Security.Audit;

public sealed record AuditVerificationResult(bool IsIntact, long? FirstTamperedSequence);

// R-SEC7: append-only, hash-chained security audit log, stored separately
// from operational telemetry (R-PER1). No Update/Delete is exposed anywhere
// on this contract by design — that is the tamper-evidence guarantee.
public interface IAuditLog
{
    Task<AuditEvent> AppendAsync(
        AuditEventKind kind,
        string detailJson,
        Guid? actorUserId = null,
        Guid? projectId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<AuditEvent>> ReadAsync(long fromSequence = 0, int limit = 100, CancellationToken ct = default);

    // Recomputes the hash chain from the first record and confirms every
    // RecordHash matches its recomputed value and every PriorHash matches the
    // preceding record's RecordHash. Detects any row edited/deleted/inserted
    // outside this log's own Append path.
    Task<AuditVerificationResult> VerifyChainAsync(CancellationToken ct = default);
}
