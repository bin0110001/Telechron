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

    // R-MOD6a: unloads only after the caller has confirmed zero in-flight
    // references (that's the drain coordinator's job, not this method's --
    // this method just does the actual ALC.Unload() + GC + leak check).
    Task<ModuleUnloadResult> UnloadAsync(string moduleName, CancellationToken ct = default);
}
