namespace Telechron.Agent.Dispatch;

// Maps CommandKind -> handler, so AgentConnectionWorker's dispatch loop
// doesn't need a big switch statement and adding a new command kind
// doesn't require touching the connection/heartbeat plumbing.
public sealed class CommandHandlerRegistry(IEnumerable<ICommandHandler> handlers)
{
    private readonly IReadOnlyDictionary<string, ICommandHandler> _byKind =
        handlers.ToDictionary(h => h.CommandKind);

    public ICommandHandler? Find(string commandKind) => _byKind.GetValueOrDefault(commandKind);
}
