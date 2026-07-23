namespace Telechron.Host.Modules.Health;

// R-MOD1: "Modules support... health monitoring." Distinct from
// IModuleCanaryObserver (Runtime/) -- the canary is a bounded,
// post-hot-reload window that ends and triggers rollback; this is an
// ongoing rolling-window health signal for a module's entire loaded
// lifetime, queryable at any time (e.g. for a status dashboard or to
// decide whether to route new work to a module at all).
public interface IModuleHealthMonitor
{
    void RecordInvocationOutcome(string moduleName, bool succeeded);
    ModuleHealthStatus GetStatus(string moduleName);
    void Reset(string moduleName);
}
