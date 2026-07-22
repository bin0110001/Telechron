namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for Run (R-DM2).
public sealed class RunEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? MachineId { get; set; }
    public int Status { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? LastHeartbeatUtc { get; set; }
    public string? SuiteResultsJson { get; set; }
    public string? LogsRef { get; set; }

    public ProjectEntity? Project { get; set; }
    public MachineEntity? Machine { get; set; }
}
