using Microsoft.Extensions.Logging;
using Telechron.Host.Modules.Health;

namespace Telechron.Host.Modules.Runtime;

public sealed class ModuleHotReloadCoordinator(
    IModuleDrainCoordinator drainCoordinator,
    IModuleRuntime moduleRuntime,
    IModuleCanaryObserver canaryObserver,
    IModuleHealthMonitor healthMonitor,
    ILogger<ModuleHotReloadCoordinator> logger) : IModuleHotReloadCoordinator
{
    public async Task<ModuleHotReloadResult> ReloadAsync(
        string moduleName, string newAssemblyPath, string outgoingAssemblyPath, TimeSpan drainTimeout, CancellationToken ct = default)
    {
        // Phase 1: drain the outgoing version -- stop new dispatch, wait
        // for in-flight work to finish or hit the bounded timeout.
        var drainResult = await drainCoordinator.StartDrainAsync(moduleName, drainTimeout, ct);

        // Phase 2: unload the outgoing version only now, whether it
        // drained cleanly or timed out -- a timed-out drain still means
        // "stop waiting," not "never unload." The leak guard here is what
        // R-MOD6a's last sentence asks for: tracked per cycle, alerted on.
        var unloadResult = await moduleRuntime.UnloadAsync(moduleName, ct);
        if (unloadResult.LeakDetected)
        {
            logger.LogError(
                "Module {ModuleName} hot-reload: outgoing version ALC did not unload cleanly -- retained-reference leak (R-MOD6a).",
                moduleName);
        }

        LoadedModule newVersion;
        try
        {
            newVersion = await moduleRuntime.LoadAsync(newAssemblyPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Module {ModuleName} hot-reload: new version failed to load -- rolling back to outgoing assembly.", moduleName);
            await moduleRuntime.LoadAsync(outgoingAssemblyPath, ct);
            return new ModuleHotReloadResult(
                ModuleHotReloadOutcome.RolledBackAfterLoadFailure, drainResult, unloadResult.LeakDetected, CanaryResult: null,
                $"New version failed to load: {ex.Message}");
        }

        // R-MOD1: a new version starts its own ongoing health history --
        // the outgoing version's failure record must not carry over and
        // immediately mark the freshly-loaded version Degraded.
        healthMonitor.Reset(moduleName);

        // Canary/observation window: a module can pass its self-test yet
        // fail on real inputs (R-MOD6a). RecordInvocationOutcome is called
        // by the real dispatch site as traffic flows to newVersion during
        // this window (Phase 6 wires the actual invocation path) -- both
        // here and on IModuleHealthMonitor, since the canary window is a
        // bounded post-reload check while the health monitor's rolling
        // window covers the module's entire loaded lifetime afterward.
        canaryObserver.StartWindow(moduleName);
        var canaryResult = await canaryObserver.EvaluateAsync(moduleName, ct);

        if (canaryResult.Outcome == CanaryOutcome.RolledBack)
        {
            logger.LogWarning(
                "Module {ModuleName} hot-reload: canary failed ({Reason}) -- rolling back to outgoing version.",
                moduleName, canaryResult.Reason);

            var rollbackUnload = await moduleRuntime.UnloadAsync(moduleName, ct);
            if (rollbackUnload.LeakDetected)
            {
                logger.LogError(
                    "Module {ModuleName} hot-reload rollback: failed new version's ALC did not unload cleanly -- retained-reference leak (R-MOD6a).",
                    moduleName);
            }
            await moduleRuntime.LoadAsync(outgoingAssemblyPath, ct);

            return new ModuleHotReloadResult(
                ModuleHotReloadOutcome.RolledBackAfterCanaryFailure, drainResult,
                unloadResult.LeakDetected || rollbackUnload.LeakDetected, canaryResult, canaryResult.Reason);
        }

        logger.LogInformation("Module {ModuleName} hot-reload succeeded -- new version {Version} is live.", moduleName, newVersion.Version);
        return new ModuleHotReloadResult(
            ModuleHotReloadOutcome.ReloadedSuccessfully, drainResult, unloadResult.LeakDetected, canaryResult,
            "Drained, reloaded, and canary window passed.");
    }
}
