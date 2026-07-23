namespace Telechron.Host.Modules.Runtime;

// R-MOD6a: "a bounded canary/observation window with automatic rollback
// to the prior version on an elevated error rate."
public sealed class ModuleCanaryOptions
{
    public TimeSpan WindowDuration { get; set; } = TimeSpan.FromMinutes(5);

    // Below this many observed invocations, the failure rate isn't a
    // statistically meaningful signal yet -- a module invoked once during
    // the window that happens to fail shouldn't roll back a otherwise-fine
    // release on a sample size of one.
    public int MinimumInvocationsBeforeEvaluating { get; set; } = 5;

    // "Elevated error rate" threshold -- exceeding this over the window
    // (once MinimumInvocationsBeforeEvaluating is met) triggers rollback.
    public double MaxFailureRate { get; set; } = 0.5;
}
