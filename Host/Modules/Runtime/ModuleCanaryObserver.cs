using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Telechron.Host.Modules.Runtime;

public sealed class ModuleCanaryObserver(IOptions<ModuleCanaryOptions> options, ILogger<ModuleCanaryObserver> logger)
    : IModuleCanaryObserver
{
    private sealed class WindowState
    {
        public required DateTimeOffset StartedAtUtc { get; init; }
        public int Total;
        public int Failed;
    }

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private readonly ConcurrentDictionary<string, WindowState> _windows = new();

    public void StartWindow(string moduleName) =>
        _windows[moduleName] = new WindowState { StartedAtUtc = DateTimeOffset.UtcNow };

    public void RecordInvocationOutcome(string moduleName, bool succeeded)
    {
        if (!_windows.TryGetValue(moduleName, out var state))
            return; // no active window -- outside canary observation, not an error

        Interlocked.Increment(ref state.Total);
        if (!succeeded)
            Interlocked.Increment(ref state.Failed);
    }

    public async Task<CanaryWindowResult> EvaluateAsync(string moduleName, CancellationToken ct = default)
    {
        var opts = options.Value;
        if (!_windows.TryGetValue(moduleName, out var state))
            throw new InvalidOperationException($"No active canary window for module '{moduleName}' -- call StartWindow first.");

        try
        {
            while (DateTimeOffset.UtcNow - state.StartedAtUtc < opts.WindowDuration)
            {
                if (HasElevatedFailureRate(state, opts, out var earlyReason))
                {
                    logger.LogWarning("Module {ModuleName} canary rolled back early: {Reason}", moduleName, earlyReason);
                    return new CanaryWindowResult(CanaryOutcome.RolledBack, state.Total, state.Failed, earlyReason);
                }

                await Task.Delay(PollInterval, ct);
            }
        }
        finally
        {
            _windows.TryRemove(moduleName, out _);
        }

        if (HasElevatedFailureRate(state, opts, out var reason))
        {
            logger.LogWarning("Module {ModuleName} canary rolled back at window end: {Reason}", moduleName, reason);
            return new CanaryWindowResult(CanaryOutcome.RolledBack, state.Total, state.Failed, reason);
        }

        logger.LogInformation(
            "Module {ModuleName} canary window healthy: {Failed}/{Total} failures.", moduleName, state.Failed, state.Total);
        return new CanaryWindowResult(CanaryOutcome.Healthy, state.Total, state.Failed, "Failure rate within threshold (or too few invocations to evaluate).");
    }

    private static bool HasElevatedFailureRate(WindowState state, ModuleCanaryOptions opts, out string reason)
    {
        if (state.Total < opts.MinimumInvocationsBeforeEvaluating)
        {
            reason = string.Empty;
            return false;
        }

        var failureRate = (double)state.Failed / state.Total;
        if (failureRate > opts.MaxFailureRate)
        {
            reason = $"{state.Failed}/{state.Total} invocations failed ({failureRate:P0}), exceeding the {opts.MaxFailureRate:P0} threshold.";
            return true;
        }

        reason = string.Empty;
        return false;
    }
}
