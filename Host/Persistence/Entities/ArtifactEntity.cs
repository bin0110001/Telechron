namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for Artifact (R-DM13, R-PER7).
public sealed class ArtifactEntity
{
    public Guid Id { get; set; }
    public Guid? WorkflowRunId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = string.Empty;
    public string BlobRef { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public WorkflowRunEntity? WorkflowRun { get; set; }
}
