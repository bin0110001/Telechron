namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for Workflow (R-DM5).
public sealed class WorkflowEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DefinitionJson { get; set; } = string.Empty;
    public int FailurePolicy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public ProjectEntity? Project { get; set; }
}
