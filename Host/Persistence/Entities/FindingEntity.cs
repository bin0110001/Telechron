namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for Finding (R-DM3, R-FIX8).
public sealed class FindingEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? RunId { get; set; }
    public Guid? WorkflowRunId { get; set; }
    public string? OriginFilePath { get; set; }
    public string RootCauseSignature { get; set; } = string.Empty;
    public int Severity { get; set; }
    public string Category { get; set; } = string.Empty;
    public int FailureClass { get; set; }
    public string? Fixability { get; set; }
    public string? FixStatus { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public ProjectEntity? Project { get; set; }
    public RunEntity? Run { get; set; }
}
