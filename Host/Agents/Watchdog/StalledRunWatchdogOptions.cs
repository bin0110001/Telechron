namespace Telechron.Host.Agents.Watchdog;

// R-SCH5: "Agent disconnect during an active Run triggers a bounded
// grace/reconnect window before the Run is marked Stalled." GraceWindow is
// that bound -- a Run whose LastHeartbeatUtc is older than this is
// considered stalled, not merely a missed heartbeat tick.
public sealed class StalledRunWatchdogOptions
{
    public TimeSpan GraceWindow { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromSeconds(30);
}
