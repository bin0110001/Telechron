namespace Telechron.Sdk.Domain;

// R-DM16 revision history entry — append-only, never edited/deleted once
// written (mirrors the immutability the repair-lineage chain relies on,
// R-DM3a). ChangedByUserId is the human who approved the edit (R-DM16b: edits
// are always a privileged-path human approval, never autonomous) — it is
// non-nullable because every revision, even one proposed by Repair/Synthesis,
// only becomes a revision at all once a human approves it.
public sealed record RequirementRevision
{
    public required Guid Id { get; init; }
    public required Guid RequirementId { get; init; }
    public required int RevisionNumber { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required RequirementStatus Status { get; init; }
    public required Guid ChangedByUserId { get; init; }
    public required string ChangeReason { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
