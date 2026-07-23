using System.Collections.Concurrent;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Agents.Dispatch;

// In-memory, single-Host-instance correlator — same scoping constraint as
// InMemoryDispatchQueue (R-REL4 documents the Host as a singleton for v1).
public sealed class InMemoryCommandResultCorrelator : ICommandResultCorrelator
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<CommandOutcome>> _pending = new();

    public async Task<CommandOutcome> AwaitResultAsync(Guid commandId, Func<Task> dispatch, TimeSpan timeout, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<CommandOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(commandId, tcs))
            throw new InvalidOperationException($"A wait is already registered for command {commandId}.");

        try
        {
            // Registered before dispatching so a very-fast Agent report can
            // never arrive before this correlator is ready to receive it.
            await dispatch();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            await using var registration = timeoutCts.Token.Register(
                static state => ((TaskCompletionSource<CommandOutcome>)state!).TrySetCanceled(), tcs);

            try
            {
                return await tcs.Task;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException($"No result reported for command {commandId} within {timeout}.");
            }
        }
        finally
        {
            _pending.TryRemove(commandId, out _);
        }
    }

    public void Complete(CommandOutcome outcome)
    {
        if (_pending.TryGetValue(outcome.CommandId, out var tcs))
            tcs.TrySetResult(outcome);
    }
}
