using System.Collections.Concurrent;
using Telechron.Sdk.Repair;

namespace Telechron.Host.Repair;

// Default R-FIX9 implementation: one SemaphoreSlim(1,1) per project root
// path, so concurrent repair pipeline instances against the SAME project
// queue rather than interleave Snapshot/Apply/Verify/Commit against a
// shared working tree, while unrelated projects proceed independently.
//
// R-FIX9's second half -- mutual exclusion between a repair pipeline
// instance and IModuleDrainCoordinator's hot-reload drain on the same
// module ID -- is NOT wired up here. This gate's key space is project
// root paths; ModuleDrainCoordinator's is module names. Cross-referencing
// them only matters when a repair patch actually touches a loaded
// module's own assembly, which doesn't happen until Phase 8's
// module-synthesis path exists to produce such a patch in the first
// place. Left as an explicit gap rather than a half-built shared registry.
public sealed class RepairConcurrencyGate : IRepairConcurrencyGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IAsyncDisposable> AcquireAsync(string projectRootPath, CancellationToken ct = default)
    {
        var normalizedPath = Path.GetFullPath(projectRootPath);
        var semaphore = _locks.GetOrAdd(normalizedPath, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(ct);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
