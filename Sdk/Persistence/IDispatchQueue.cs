using Telechron.Sdk.Domain;
using Telechron.Sdk.Security;

namespace Telechron.Sdk.Persistence;

// Host-side in-memory dispatch fan-out: one channel per connected Agent
// (Machine), fed by whatever schedules work (Phase 9) and drained by each
// Agent's SubscribeToDispatch gRPC stream. Not persisted — a dispatched-but-
// unacknowledged command is the Run's own durable state's problem to retry,
// not this queue's (R-SCH5's reconnect/resume grace window is what actually
// makes redelivery safe).
//
// R-SEC2: Enqueue validates every command against ICommandDispatchValidator
// before it can ever reach an Agent — this is a structural guarantee (the
// only implementation validates internally), not a convention callers must
// remember to follow, mirroring how Phase 2's IPermissionMediator is the
// single non-bypassable checkpoint for capability access.
public interface IDispatchQueue
{
    CommandValidationResult Enqueue(Guid machineId, DispatchedCommand command);

    IAsyncEnumerable<DispatchedCommand> ReadAllAsync(Guid machineId, CancellationToken ct);
}

public sealed record DispatchedCommand(
    Guid CommandId,
    Guid RunId,
    string CommandKind,
    string ParametersJson,
    string ToolchainImageDigest);
