namespace Telechron.Sdk.Scheduling;

public sealed record ScheduleDefinition
{
    public required Guid Id { get; init; }
    public required Guid WorkflowId { get; init; }
    public required Guid ProjectId { get; init; }
    public Guid? MachineId { get; init; }
    public required string CronExpression { get; init; }
    public required bool IsEnabled { get; init; }
    public required bool SerializePerMachine { get; init; } = true;
    public required bool SerializePerProject { get; init; } = true;
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? LastFiredAtUtc { get; init; }
}
