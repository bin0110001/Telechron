namespace Telechron.Sdk.Persistence;

// Bridges the async, Agent-initiated ReportCommandResult RPC back to a
// Host-side caller that dispatched a command and needs its outcome
// synchronously (from the caller's perspective) — e.g. a module self-test
// gating hot-reload (R-MOD4a/R-MOD5b), or Phase 7's repair-verify stage.
// One outstanding wait per CommandId; a second RegisterAsync for the same
// CommandId before the first resolves is a caller bug, not a valid retry
// pattern (dispatch a new CommandId to retry).
public interface ICommandResultCorrelator
{
    // Registers interest in a CommandId's outcome, then dispatches (via the
    // supplied callback, invoked after registration so no result can race
    // ahead of the wait being ready) and awaits either the Agent's report or
    // the timeout.
    Task<CommandOutcome> AwaitResultAsync(Guid commandId, Func<Task> dispatch, TimeSpan timeout, CancellationToken ct = default);

    // Called by AgentServiceImpl.ReportCommandResult — completes the
    // matching pending wait, if any. A report for a CommandId nobody is
    // waiting on (already timed out, or a fire-and-forget dispatch) is a
    // harmless no-op.
    void Complete(CommandOutcome outcome);
}
