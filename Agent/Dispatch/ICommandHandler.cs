using Telechron.Sdk.Grpc;

namespace Telechron.Agent.Dispatch;

public sealed record CommandHandlerResult(bool Succeeded, string OutputSummary, string ErrorMessage)
{
    public static CommandHandlerResult Success(string summary) => new(true, summary, string.Empty);
    public static CommandHandlerResult Failure(string error) => new(false, string.Empty, error);
}

// One handler per CommandKind (Host/Agents/Dispatch/CommandKinds.cs) --
// registered by kind string so AgentConnectionWorker can dispatch without
// a big switch statement, and so kinds without a handler yet (run-tests,
// build, execute-workflow-step, apply-repair-patch -- Phases 6/7) degrade
// to a clear "unhandled" result instead of being silently dropped.
public interface ICommandHandler
{
    string CommandKind { get; }

    Task<CommandHandlerResult> HandleAsync(
        CommandDispatch command, AgentService.AgentServiceClient client, string machineId, string sessionToken,
        CancellationToken ct = default);
}
