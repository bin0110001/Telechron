namespace Telechron.Sdk.Domain;

// R-DM16: a Project's living requirements/architectural-intent record. One
// per Project (including Telechron's own reflexive Project — R-DM16a). The
// document itself is just an anchor; its content is the set of Requirements
// (and their RequirementRevision history) that reference it.
public sealed record DesignDocument
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required string Title { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
