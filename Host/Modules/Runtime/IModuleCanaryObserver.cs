namespace Telechron.Host.Modules.Runtime;

// R-MOD6a: "Post-hot-reload, the module runs a bounded canary/observation
// window with automatic rollback to the prior version on an elevated
// error rate (a module can pass its self-test yet fail on real inputs)."
// RecordInvocationOutcome is called by every real dispatch against the
// new version during the window (Phase 6 wires the real invocation site;
// self-test re-runs are the only available concrete caller in Phase 5).
// EvaluateAsync blocks until the window elapses or an early rollback
// verdict is reached, whichever comes first -- a canary that's already
// clearly failing shouldn't keep taking live traffic for the full window
// just to confirm what's already obvious.
public interface IModuleCanaryObserver
{
    void StartWindow(string moduleName);
    void RecordInvocationOutcome(string moduleName, bool succeeded);
    Task<CanaryWindowResult> EvaluateAsync(string moduleName, CancellationToken ct = default);
}
