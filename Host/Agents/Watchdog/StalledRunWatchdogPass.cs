using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Agents.Watchdog;

// R-REL1/R-SCH5: "Agent disconnect during an active Run triggers a bounded
// grace/reconnect window before the Run is marked Stalled... on reconnect
// within the window the Run resumes rather than restarting." This pass is
// the "mark Stalled" half; AgentServiceImpl.Heartbeat (Host/Agents/Grpc)
// is the "resume within the window" half -- a Run only ever moves
// Running -> Stalled here, and Stalled -> Running there, never the other
// direction from either side.
public sealed class StalledRunWatchdogPass(
    IRunRepository runRepository, IOptions<StalledRunWatchdogOptions> options, ILogger<StalledRunWatchdogPass> logger)
{
    public async Task<int> ScanAsync(CancellationToken ct = default)
    {
        var active = await runRepository.GetActiveAsync(ct);
        var cutoff = DateTimeOffset.UtcNow - options.Value.GraceWindow;
        var stalledCount = 0;

        foreach (var run in active)
        {
            if (run.Status != RunStatus.Running)
                continue;

            // A Run that hasn't sent its first heartbeat yet is graced from
            // StartedAtUtc, not treated as immediately stalled -- the Agent
            // may not have completed its first heartbeat tick yet.
            var lastActivity = run.LastHeartbeatUtc ?? run.StartedAtUtc;
            if (lastActivity is null || lastActivity >= cutoff)
                continue;

            await runRepository.UpdateAsync(run with { Status = RunStatus.Stalled }, ct);
            stalledCount++;
            logger.LogWarning(
                "Run {RunId} marked Stalled -- no heartbeat since {LastActivity} (grace window {GraceWindow}).",
                run.Id, lastActivity, options.Value.GraceWindow);
        }

        return stalledCount;
    }
}
