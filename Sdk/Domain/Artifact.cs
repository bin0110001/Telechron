namespace Telechron.Sdk.Domain;

// Typed workflow output (R-DM13). BlobRef points outside SQLite (R-PER7:
// "Binary Artifacts are stored OUTSIDE SQLite... with only metadata/references
// persisted in the DB") — never a byte[] payload field here.
public sealed record Artifact
{
    public required Guid Id { get; init; }
    public Guid? WorkflowRunId { get; init; }
    public required string Name { get; init; }
    public required string ArtifactType { get; init; }
    public required string BlobRef { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
