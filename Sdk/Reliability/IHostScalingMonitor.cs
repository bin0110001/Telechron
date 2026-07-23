namespace Telechron.Sdk.Reliability;

public sealed record HostScalingStatus
{
    public required int ActiveAgentsCount { get; init; }
    public required int WorkflowsPerMinute { get; init; }
    public required long WriteThroughputBytesPerSec { get; init; }
    public required bool NearingCeiling { get; init; }
    public string? DatabaseMigrationAdvice { get; init; }
}

public interface IHostScalingMonitor
{
    Task<HostScalingStatus> EvaluateScalingStatusAsync(CancellationToken ct = default);
}
