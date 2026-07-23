namespace Telechron.Host.Modules.Health;

public sealed class ModuleHealthMonitorOptions
{
    // Invocations older than this fall out of the rolling window -- an
    // old failure shouldn't keep a module marked Degraded forever.
    public TimeSpan RollingWindow { get; set; } = TimeSpan.FromMinutes(15);
    public int MinimumInvocationsBeforeEvaluating { get; set; } = 5;
    public double DegradedFailureRateThreshold { get; set; } = 0.2;
}
