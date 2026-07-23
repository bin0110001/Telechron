using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules.Runtime;

public sealed record ModuleUnloadResult(bool Unloaded, bool LeakDetected);

// R-SYS4: "Modules are never compile-time dependencies of the Host and are
// only accessed through the module runtime." This interface is that
// access point -- Host code never does `new SomeModule()` or holds a
// module-project reference; it always goes through Load/GetLoaded/Invoke
// here.
public interface IModuleRuntime
{
    Task<LoadedModule> LoadAsync(string moduleAssemblyPath, CancellationToken ct = default);

    LoadedModule? GetLoaded(string moduleName);

    // Typed access to a loaded module as one of the provider-specific
    // subtypes (IToolchainModule, ITestRunnerModule, IFunctionExecutorModule,
    // IConnectorModule, ILlmEngineModule -- Phase 6). Returns null if the
    // module isn't loaded or doesn't implement TModule, so callers get a
    // clear "not available" rather than an InvalidCastException. This is
    // in-process invocation of the module's OWN descriptive/interpretive
    // logic (parsing output, describing commands, declaring auth needs) --
    // distinct from executing untrusted external code, which always goes
    // through a container per R-SYS6.
    TModule? GetLoadedAs<TModule>(string moduleName) where TModule : class, IModule;

    // R-MOD6a: unloads only after the caller has confirmed zero in-flight
    // references (that's the drain coordinator's job, not this method's --
    // this method just does the actual ALC.Unload() + GC + leak check).
    Task<ModuleUnloadResult> UnloadAsync(string moduleName, CancellationToken ct = default);
}
