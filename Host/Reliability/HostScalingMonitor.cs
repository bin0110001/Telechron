namespace Telechron.Host.Reliability;

using Telechron.Sdk.Reliability;

public sealed class HostScalingMonitor : IHostScalingMonitor
{
    private const int MaxAgentsLimit = 50;
    private const int MaxWorkflowsPerMinLimit = 100;
    private const long MaxWriteThroughputLimit = 10 * 1024 * 1024; // 10 MB/s

    public Task<HostScalingStatus> EvaluateScalingStatusAsync(CancellationToken ct = default)
    {
        // Simulated current load metrics
        var activeAgents = 10;
        var workflowsPerMin = 20;
        var writeThroughput = 2 * 1024 * 1024L;

        var nearingCeiling = activeAgents >= MaxAgentsLimit * 0.8
            || workflowsPerMin >= MaxWorkflowsPerMinLimit * 0.8
            || writeThroughput >= MaxWriteThroughputLimit * 0.8;

        string? migrationAdvice = null;
        if (nearingCeiling)
        {
            migrationAdvice = "System is approaching SQLite scaling ceiling (R-REL4). Recommend migrating persistence to a networked RDBMS (PostgreSQL/SQL Server).";
        }

        return Task.FromResult(new HostScalingStatus
        {
            ActiveAgentsCount = activeAgents,
            WorkflowsPerMinute = workflowsPerMin,
            WriteThroughputBytesPerSec = writeThroughput,
            NearingCeiling = nearingCeiling,
            DatabaseMigrationAdvice = migrationAdvice
        });
    }
}
