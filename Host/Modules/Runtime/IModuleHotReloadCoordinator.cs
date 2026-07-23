namespace Telechron.Host.Modules.Runtime;

// R-MOD6/R-MOD6a end to end: drains the outgoing version, loads the new
// one, runs the canary window, and rolls back to the outgoing version's
// assembly on an elevated error rate -- the orchestration point that ties
// InFlightInvocationTracker, IModuleDrainCoordinator, IModuleRuntime, and
// IModuleCanaryObserver together. Assumes the new version has already
// passed IModuleTrustEvaluator (R-MOD5a/b, R-MOD4a, R-MOD8) -- hot-reload
// is a lifecycle operation on an already-trusted version, not a second
// place trust gets decided.
public interface IModuleHotReloadCoordinator
{
    Task<ModuleHotReloadResult> ReloadAsync(
        string moduleName, string newAssemblyPath, string outgoingAssemblyPath, TimeSpan drainTimeout, CancellationToken ct = default);
}
