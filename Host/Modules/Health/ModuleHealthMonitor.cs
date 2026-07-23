using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Telechron.Host.Modules.Health;

public sealed class ModuleHealthMonitor(IOptions<ModuleHealthMonitorOptions> options) : IModuleHealthMonitor
{
    private sealed record InvocationRecord(DateTimeOffset AtUtc, bool Succeeded);

    // Bounded per-module queue -- old entries are trimmed by
    // RollingWindow on every read, not just on write, so a module that
    // stops receiving traffic entirely still correctly ages out of
    // Degraded rather than freezing at its last observed state.
    private readonly ConcurrentDictionary<string, ConcurrentQueue<InvocationRecord>> _byModule = new();

    public void RecordInvocationOutcome(string moduleName, bool succeeded)
    {
        var queue = _byModule.GetOrAdd(moduleName, static _ => new ConcurrentQueue<InvocationRecord>());
        queue.Enqueue(new InvocationRecord(DateTimeOffset.UtcNow, succeeded));
    }

    public ModuleHealthStatus GetStatus(string moduleName)
    {
        if (!_byModule.TryGetValue(moduleName, out var queue))
            return new ModuleHealthStatus(ModuleHealthState.Unknown, 0, 0, null);

        var cutoff = DateTimeOffset.UtcNow - options.Value.RollingWindow;
        TrimExpired(queue, cutoff);

        var records = queue.ToArray();
        if (records.Length == 0)
            return new ModuleHealthStatus(ModuleHealthState.Unknown, 0, 0, null);

        var failed = records.Count(r => !r.Succeeded);
        var lastAt = records.Max(r => r.AtUtc);

        if (records.Length < options.Value.MinimumInvocationsBeforeEvaluating)
            return new ModuleHealthStatus(ModuleHealthState.Healthy, records.Length, failed, lastAt);

        var failureRate = (double)failed / records.Length;
        var state = failureRate > options.Value.DegradedFailureRateThreshold ? ModuleHealthState.Degraded : ModuleHealthState.Healthy;
        return new ModuleHealthStatus(state, records.Length, failed, lastAt);
    }

    public void Reset(string moduleName) => _byModule.TryRemove(moduleName, out _);

    private static void TrimExpired(ConcurrentQueue<InvocationRecord> queue, DateTimeOffset cutoff)
    {
        while (queue.TryPeek(out var oldest) && oldest.AtUtc < cutoff)
        {
            queue.TryDequeue(out _);
        }
    }
}
