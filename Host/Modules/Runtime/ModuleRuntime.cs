using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules.Runtime;

// R-MOD7/R-SYS4: loads each module into its own collectible ALC, tracks
// loaded instances by name, and unloads with a leak guard (R-MOD6a).
// Concurrency: a plain lock is fine at Host scale -- module load/unload is
// an infrequent administrative operation, never a per-request hot path.
public sealed class ModuleRuntime(ILogger<ModuleRuntime> logger) : IModuleRuntime
{
    private readonly ConcurrentDictionary<string, LoadedModule> _loaded = new();

    public Task<LoadedModule> LoadAsync(string moduleAssemblyPath, CancellationToken ct = default)
    {
        var context = new ModuleLoadContext(moduleAssemblyPath);
        var assembly = context.LoadFromAssemblyPath(moduleAssemblyPath);

        var moduleType = assembly.GetTypes().FirstOrDefault(t =>
            typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
        if (moduleType is null)
        {
            context.Unload();
            throw new InvalidOperationException(
                $"Assembly '{moduleAssemblyPath}' does not contain a public non-abstract type implementing {nameof(IModule)}.");
        }

        if (Activator.CreateInstance(moduleType) is not IModule instance)
        {
            context.Unload();
            throw new InvalidOperationException($"Type '{moduleType.FullName}' could not be instantiated as an {nameof(IModule)}.");
        }

        var loaded = new LoadedModule
        {
            ModuleName = instance.Name,
            Version = instance.Version,
            Instance = instance,
            LoadContext = context,
            UnloadWeakReference = new WeakReference(context, trackResurrection: true),
            LoadedAtUtc = DateTimeOffset.UtcNow,
        };

        _loaded[instance.Name] = loaded;
        logger.LogInformation("Module {ModuleName} v{Version} loaded.", instance.Name, instance.Version);
        return Task.FromResult(loaded);
    }

    public LoadedModule? GetLoaded(string moduleName) =>
        _loaded.TryGetValue(moduleName, out var loaded) ? loaded : null;

    public async Task<ModuleUnloadResult> UnloadAsync(string moduleName, CancellationToken ct = default)
    {
        if (!_loaded.TryRemove(moduleName, out var loaded))
            return new ModuleUnloadResult(Unloaded: false, LeakDetected: false);

        var weakRef = loaded.UnloadWeakReference;
        loaded.LoadContext.Unload();
        // R-MOD6a: .NET's ALC.Unload() is a request, not a guarantee -- it
        // only actually collects once every strong reference (including
        // `loaded`/its `LoadContext` here) is gone. Drop the local before
        // GC'ing, or this loop would spin until the attempt cap purely
        // because of its own still-live reference, masking real leaks and
        // false-negative-ing genuine ones alike.
        loaded = null;

        for (var attempt = 0; attempt < 10 && weakRef.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(50, ct);
        }

        var leaked = weakRef.IsAlive;
        if (leaked)
            logger.LogWarning("Module {ModuleName} ALC did not unload -- retained-reference leak detected (R-MOD6a).", moduleName);
        else
            logger.LogInformation("Module {ModuleName} unloaded cleanly.", moduleName);

        return new ModuleUnloadResult(Unloaded: true, LeakDetected: leaked);
    }
}
