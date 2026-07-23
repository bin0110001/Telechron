namespace Telechron.Sdk.Domain;

// Represents test execution (R-DM2). LogsRef points outside SQLite (same
// out-of-DB pattern as Artifact/Module blobs, R-PER7) rather than storing raw
// log text. LastHeartbeatUtc backs R-RUN3 ("Runs emit heartbeats while
// active") and the stalled-run watchdog (R-REL1/R-SCH5, Host/Agents/Watchdog).
public sealed record Run
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public Guid? MachineId { get; init; }
    public required RunStatus Status { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public DateTimeOffset? LastHeartbeatUtc { get; init; }
    public string? SuiteResultsJson { get; init; }
    public string? LogsRef { get; init; }
}
