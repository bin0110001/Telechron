namespace Telechron.Sdk.Domain;

// R-DM16: current state of one Requirement entry within a Design Document,
// identified by a stable Requirement ID matching the R-XXX convention (e.g.
// "R-DM16" itself). Title/Body reflect the CURRENT revision; history lives in
// RequirementRevision (R-DM16b — edits create a new revision, never overwrite).
public sealed record Requirement
{
    public required Guid Id { get; init; }
    public required Guid DesignDocumentId { get; init; }
    public required string RequirementId { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required RequirementStatus Status { get; init; }
    public required int CurrentRevisionNumber { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
