namespace Telechron.Host.Modules.Runtime;

// R-MOD6/R-MOD6a: the two-phase drain protocol. Phase 1 (StartDrainAsync)
// stops new dispatch and waits for in-flight work to finish or hit the
// bounded timeout (then those are considered cancelled, not waited on
// further -- this coordinator doesn't itself cancel in-flight work, that's
// the caller/module invocation site's responsibility since only it knows
// how to cancel its own call). Phase 2 (unload, via IModuleRuntime) only
// proceeds once phase 1 reports the module drained -- calling unload
// before drain completes would defeat the whole point of draining.
public interface IModuleDrainCoordinator
{
    Task<ModuleDrainResult> StartDrainAsync(string moduleName, TimeSpan timeout, CancellationToken ct = default);
}
