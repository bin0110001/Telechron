using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules.Runtime;

// R-MOD7: one loaded module version -- its own ALC, its own IModule
// instance. UnloadWeakReference is what the leak guard (R-MOD6a) checks
// after Unload() to confirm the ALC was actually collected, not just
// requested to unload -- .NET's Unload() is a request, not a guarantee,
// if any reference (module instance, delegate, thread with the ALC's
// assemblies on its stack) is still reachable.
public sealed class LoadedModule
{
    public required string ModuleName { get; init; }
    public required ModuleVersion Version { get; init; }
    public required IModule Instance { get; init; }
    public required ModuleLoadContext LoadContext { get; init; }
    public required WeakReference UnloadWeakReference { get; init; }
    public required DateTimeOffset LoadedAtUtc { get; init; }
}
