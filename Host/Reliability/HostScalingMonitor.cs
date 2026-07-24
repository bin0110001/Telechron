namespace Telechron.Host.Reliability;

using Telechron.Sdk.Persistence;
using Telechron.Sdk.Reliability;

// R-REL4: reads real counts from the persistence layer rather than
// simulated constants -- otherwise this can never actually detect a Host
// approaching its documented SQLite scaling ceiling.
public sealed class HostScalingMonitor(
    IAgentSessionRepository agentSessionRepository,
    IWorkflowRunRepository workflowRunRepository) : IHostScalingMonitor
{
    private const int MaxAgentsLimit = 50;
    private const int MaxWorkflowsPerMinLimit = 100;
    private const long MaxWriteThroughputLimit = 10 * 1024 * 1024; // 10 MB/s

    public async Task<HostScalingStatus> EvaluateScalingStatusAsync(CancellationToken ct = default)
    {
        var allSessions = await agentSessionRepository.GetAllAsync(ct);
        var activeAgents = allSessions.Count(s => s.ExpiresAtUtc > DateTimeOffset.UtcNow);

        var activeWorkflowRuns = await workflowRunRepository.GetActiveAsync(ct);
        var recentlyStarted = activeWorkflowRuns.Count(r => r.StartedAtUtc is { } started && (DateTimeOffset.UtcNow - started) <= TimeSpan.FromMinutes(1));

        // No write-throughput instrumentation exists yet (would require
        // hooking every DbContext.SaveChangesAsync call site) -- reporting
        // an honest 0 rather than a fabricated number; this dimension of
        // NearingCeiling can't fire until that instrumentation is built.
        const long writeThroughput = 0L;

        var nearingCeiling = activeAgents >= MaxAgentsLimit * 0.8
            || recentlyStarted >= MaxWorkflowsPerMinLimit * 0.8
            || writeThroughput >= MaxWriteThroughputLimit * 0.8;

        string? migrationAdvice = null;
        if (nearingCeiling)
        {
            migrationAdvice = "System is approaching SQLite scaling ceiling (R-REL4). Recommend migrating persistence to a networked RDBMS (PostgreSQL/SQL Server).";
        }

        return new HostScalingStatus
        {
            ActiveAgentsCount = activeAgents,
            WorkflowsPerMinute = recentlyStarted,
            WriteThroughputBytesPerSec = writeThroughput,
            NearingCeiling = nearingCeiling,
            DatabaseMigrationAdvice = migrationAdvice
        };
    }
}
