namespace Telechron.Sdk.Security.Audit;

// A single hash-chained entry (R-SEC7). Sequence/PriorHash/RecordHash are
// populated by the append path — callers only supply the semantic fields.
public sealed class AuditEvent
{
    public required long Sequence { get; init; }
    public required AuditEventKind Kind { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }

    // The User who performed/approved the action, if any (R-DM15). Null for
    // system-initiated events (e.g. an autonomous repair commit).
    public Guid? ActorUserId { get; init; }

    public Guid? ProjectId { get; init; }

    // Free-form structured detail (JSON), e.g. { "secretHandle": "...", "reason": "..." }.
    // Must never itself contain a raw secret value (R-SEC1) — callers are
    // responsible for only including handles/identifiers, never plaintext.
    public required string DetailJson { get; init; }

    public required string PriorHash { get; init; }
    public required string RecordHash { get; init; }
}
