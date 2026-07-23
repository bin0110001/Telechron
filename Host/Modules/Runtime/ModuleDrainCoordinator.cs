using Microsoft.Extensions.Logging;

namespace Telechron.Host.Modules.Runtime;

public sealed class ModuleDrainCoordinator(InFlightInvocationTracker invocationTracker, ILogger<ModuleDrainCoordinator> logger)
    : IModuleDrainCoordinator
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    public async Task<ModuleDrainResult> StartDrainAsync(string moduleName, TimeSpan timeout, CancellationToken ct = default)
    {
        // Phase 1: stop dispatching new invocations to the outgoing version.
        invocationTracker.StopAcceptingNewDispatch(moduleName);
        logger.LogInformation("Module {ModuleName} drain started -- no new dispatch accepted.", moduleName);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            while (invocationTracker.GetInFlightCount(moduleName) > 0)
            {
                await Task.Delay(PollInterval, timeoutCts.Token);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            var stillInFlight = invocationTracker.GetInFlightCount(moduleName);
            logger.LogWarning(
                "Module {ModuleName} drain timed out after {Timeout} with {InFlight} invocation(s) still in flight -- treating as cancelled.",
                moduleName, timeout, stillInFlight);
            return new ModuleDrainResult(ModuleDrainOutcome.DrainTimedOutAndCancelled, stillInFlight);
        }

        logger.LogInformation("Module {ModuleName} drained cleanly -- zero in-flight invocations.", moduleName);
        return new ModuleDrainResult(ModuleDrainOutcome.DrainedCleanly, InFlightAtTimeout: 0);
    }
}
