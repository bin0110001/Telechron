namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for WorkflowRun (R-DM5, R-WF4).
public sealed class WorkflowRunEntity
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public int Status { get; set; }
    public string DefinitionSnapshotJson { get; set; } = string.Empty;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }

    public WorkflowEntity? Workflow { get; set; }
}
