using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Security;
using Telechron.Sdk.Security.Audit;

namespace Telechron.Host.Agents.Dispatch;

// One unbounded Channel<T> per Machine, created lazily on first use by
// either side (Enqueue before the Agent connects, or ReadAllAsync before
// anything is queued — both are fine). Channels are per-process, so this
// only works for a single Host instance; R-REL4's scaling ceiling already
// documents the Host as a singleton for v1.
public sealed class InMemoryDispatchQueue(ICommandDispatchValidator validator, IServiceScopeFactory scopeFactory) : IDispatchQueue
{
    private readonly ConcurrentDictionary<Guid, Channel<DispatchedCommand>> _channels = new();

    public CommandValidationResult Enqueue(Guid machineId, DispatchedCommand command)
    {
        var result = validator.Validate(command.CommandKind, command.ParametersJson);
        if (!result.IsValid)
        {
            // Fire-and-forget on purpose: Enqueue is a synchronous, hot,
            // scheduler-facing call (Phase 9) — audit logging must not make
            // dispatch rejection block on a DB write. Loss of an audit entry
            // on process crash between these two lines is an accepted gap,
            // consistent with this queue's own not-persisted design.
            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();
                await auditLog.AppendAsync(AuditEventKind.AuthorizationDenied, JsonSerializer.Serialize(new
                {
                    reason = "command_dispatch_validation_failed",
                    machineId,
                    command.CommandKind,
                    errors = result.Errors,
                }));
            });
            return result;
        }

        GetChannel(machineId).Writer.TryWrite(command);
        return CommandValidationResult.Valid;
    }

    public IAsyncEnumerable<DispatchedCommand> ReadAllAsync(Guid machineId, CancellationToken ct) =>
        GetChannel(machineId).Reader.ReadAllAsync(ct);

    private Channel<DispatchedCommand> GetChannel(Guid machineId) =>
        _channels.GetOrAdd(machineId, static _ => Channel.CreateUnbounded<DispatchedCommand>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }));
}
