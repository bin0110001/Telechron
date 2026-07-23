using global::Grpc.Core;
using Microsoft.Extensions.Options;
using Telechron.Sdk.Grpc;

namespace Telechron.Agent.Grpc;

// R-SEC2/R-RUN3/R-SCH5: registers once, then holds a heartbeat loop and a
// long-lived dispatch subscription concurrently. A dropped connection is
// retried with backoff — this is the Agent side of the grace/reconnect
// window R-SCH5 describes; the Host-side stalled-run watchdog (Phase 4's
// remaining task) is what actually enforces the grace window's timeout.
public sealed class AgentConnectionWorker(
    AgentChannelFactory channelFactory,
    IOptions<AgentGrpcOptions> options,
    ILogger<AgentConnectionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ReconnectBackoff = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConnectionCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Agent connection cycle failed; retrying in {Backoff}.", ReconnectBackoff);
                await Task.Delay(ReconnectBackoff, stoppingToken);
            }
        }
    }

    private async Task RunConnectionCycleAsync(CancellationToken ct)
    {
        using var channel = channelFactory.CreateChannel();
        var client = new AgentService.AgentServiceClient(channel);
        var opts = options.Value;

        var registration = await client.RegisterAgentAsync(new RegisterAgentRequest
        {
            EnrollmentToken = opts.EnrollmentToken,
            MachineName = opts.MachineName,
            Hostname = Environment.MachineName,
            MachineFingerprint = MachineFingerprint.Compute(),
        }, cancellationToken: ct);

        logger.LogInformation("Registered as Machine {MachineId}.", registration.MachineId);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeatTask = RunHeartbeatLoopAsync(client, registration, cts.Token);
        var dispatchTask = RunDispatchSubscriptionAsync(client, registration, cts.Token);

        var completed = await Task.WhenAny(heartbeatTask, dispatchTask);
        cts.Cancel(); // the other loop's connection is presumed dead too — restart both together
        await completed; // surface the failure that ended this cycle
    }

    private async Task RunHeartbeatLoopAsync(AgentService.AgentServiceClient client, RegisterAgentResponse registration, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(HeartbeatInterval);
        do
        {
            await client.HeartbeatAsync(new HeartbeatRequest
            {
                MachineId = registration.MachineId,
                SessionToken = registration.SessionToken,
                SentAtUtc = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            }, cancellationToken: ct);
        } while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task RunDispatchSubscriptionAsync(AgentService.AgentServiceClient client, RegisterAgentResponse registration, CancellationToken ct)
    {
        using var call = client.SubscribeToDispatch(new SubscribeToDispatchRequest
        {
            MachineId = registration.MachineId,
            SessionToken = registration.SessionToken,
        }, cancellationToken: ct);

        await foreach (var command in call.ResponseStream.ReadAllAsync(ct))
        {
            logger.LogInformation("Received dispatched command {CommandId} ({Kind}) for Run {RunId}.",
                command.CommandId, command.CommandKind, command.RunId);
            // R-SYS6 container execution boundary picks this up next.
        }
    }
}
