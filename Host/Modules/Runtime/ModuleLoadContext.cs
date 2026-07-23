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

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : nint.Zero;
    }
}
