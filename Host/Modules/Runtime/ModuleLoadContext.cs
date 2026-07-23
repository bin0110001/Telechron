using System.Reflection;
using System.Runtime.Loader;

namespace Telechron.Host.Modules.Runtime;

// R-MOD7: each module version gets its own isolated, collectible
// AssemblyLoadContext -- this is what makes hot-reload/unload possible at
// all in .NET. isCollectible=true is required for Unload() to ever free
// anything; without it this would silently never actually unload.
public sealed class ModuleLoadContext(string moduleAssemblyPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(moduleAssemblyPath);

    // Telechron.Sdk defines IModule -- the contract type both the Host
    // (running in the Default ALC) and every module reference. If a module
    // ships its own copy of Sdk.dll and this ALC loaded it, the module's
    // IModule and the Host's IModule would be two DISTINCT Type objects
    // despite identical names, and `is IModule` / IsAssignableFrom checks
    // would silently fail. Deferring to Default for this one assembly
    // keeps the contract type identity shared, which is what makes the
    // isolation boundary usable at all rather than just isolated.
    private const string SharedContractAssemblyName = "Telechron.Sdk";

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name == SharedContractAssemblyName)
            return null; // falls through to Default's already-loaded copy

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : nint.Zero;
    }
}
