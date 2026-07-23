using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Grpc;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Security.Audit;

namespace Telechron.Host.Agents.Grpc;

// R-SEC2/R-SCH3: implements the Agent<->Host contract. Every RPC beyond
// RegisterAgent requires a valid, unexpired, unrevoked session token —
// mTLS authenticates the transport connection, the session token
// authenticates *this specific registered Machine* on top of that.
public sealed class AgentServiceImpl(
    IMachineRepository machineRepository,
    IAgentSessionRepository sessionRepository,
    IDispatchQueue dispatchQueue,
    IAuditLog auditLog,
    IOptions<AgentEnrollmentOptions> enrollmentOptions) : AgentService.AgentServiceBase
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);

    public override async Task<RegisterAgentResponse> RegisterAgent(RegisterAgentRequest request, ServerCallContext context)
    {
        if (!IsValidEnrollmentToken(request.EnrollmentToken))
        {
            await auditLog.AppendAsync(AuditEventKind.AuthenticationFailed,
                System.Text.Json.JsonSerializer.Serialize(new { reason = "invalid_enrollment_token", machineName = request.MachineName }));
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid enrollment token."));
        }

        if (string.IsNullOrWhiteSpace(request.MachineFingerprint))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "machine_fingerprint is required."));

        // R-SCH3 dedup: re-registration from the same fingerprint updates the
        // existing Machine row rather than creating a duplicate.
        var existing = await machineRepository.GetByFingerprintAsync(request.MachineFingerprint, context.CancellationToken);
        Machine machine;
        if (existing is not null)
        {
            machine = existing with { Name = request.MachineName, Hostname = request.Hostname, IsOnline = true };
            await machineRepository.UpdateAsync(machine, context.CancellationToken);
        }
        else
        {
            machine = new Machine
            {
                Id = Guid.NewGuid(),
                Name = request.MachineName,
                Hostname = request.Hostname,
                MachineFingerprint = request.MachineFingerprint,
                RegisteredAtUtc = DateTimeOffset.UtcNow,
                IsOnline = true,
            };
            await machineRepository.AddAsync(machine, context.CancellationToken);
        }

        var token = AgentSessionTokenService.GenerateToken();
        var session = new AgentSession
        {
            Id = Guid.NewGuid(),
            MachineId = machine.Id,
            SessionTokenHash = AgentSessionTokenService.Hash(token),
            IssuedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(SessionLifetime),
        };
        await sessionRepository.AddAsync(session, context.CancellationToken);

        await auditLog.AppendAsync(AuditEventKind.CapabilityGranted,
            System.Text.Json.JsonSerializer.Serialize(new { evt = "agent_registered", machineId = machine.Id, machineName = machine.Name }));

        return new RegisterAgentResponse { MachineId = machine.Id.ToString(), SessionToken = token };
    }

    public override async Task<HeartbeatResponse> Heartbeat(HeartbeatRequest request, ServerCallContext context)
    {
        var machineId = await AuthenticateSessionAsync(request.MachineId, request.SessionToken, context.CancellationToken);

        var machine = await machineRepository.GetByIdAsync(machineId, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Machine not found."));
        await machineRepository.UpdateAsync(machine with { LastHeartbeatUtc = DateTimeOffset.UtcNow, IsOnline = true }, context.CancellationToken);

        return new HeartbeatResponse { Acknowledged = true };
    }

    public override async Task SubscribeToDispatch(
        SubscribeToDispatchRequest request, IServerStreamWriter<CommandDispatch> responseStream, ServerCallContext context)
    {
        var machineId = await AuthenticateSessionAsync(request.MachineId, request.SessionToken, context.CancellationToken);

        await foreach (var command in ReadDispatchAsync(machineId, context.CancellationToken))
        {
            var message = new CommandDispatch
            {
                CommandId = command.CommandId.ToString(),
                RunId = command.RunId.ToString(),
                CommandKind = command.CommandKind,
                Parameters = Struct.Parser.ParseJson(command.ParametersJson),
                ToolchainImageDigest = command.ToolchainImageDigest,
            };
            await responseStream.WriteAsync(message, context.CancellationToken);
        }
    }

    public override async Task<CommandResultResponse> ReportCommandResult(CommandResultRequest request, ServerCallContext context)
    {
        await AuthenticateSessionAsync(request.MachineId, request.SessionToken, context.CancellationToken);

        // R-DM2/R-FIX1: translating a reported result into Run/Finding state
        // transitions is Phase 7's Findings-generation concern; this RPC's
        // job ends at acknowledging receipt over the wire.
        return new CommandResultResponse { Acknowledged = true };
    }

    private IAsyncEnumerable<DispatchedCommand> ReadDispatchAsync(Guid machineId, CancellationToken ct) =>
        dispatchQueue.ReadAllAsync(machineId, ct);

    private async Task<Guid> AuthenticateSessionAsync(string machineIdText, string sessionToken, CancellationToken ct)
    {
        if (!Guid.TryParse(machineIdText, out var machineId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "machine_id is not a valid GUID."));

        var tokenHash = AgentSessionTokenService.Hash(sessionToken);
        var session = await sessionRepository.GetByTokenHashAsync(tokenHash, ct);

        if (session is null || session.MachineId != machineId || session.RevokedAtUtc is not null || session.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            await auditLog.AppendAsync(AuditEventKind.AuthorizationDenied,
                System.Text.Json.JsonSerializer.Serialize(new { reason = "invalid_or_expired_session", machineId = machineIdText }), ct: ct);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or expired session."));
        }

        return machineId;
    }

    private bool IsValidEnrollmentToken(string presented) =>
        !string.IsNullOrEmpty(enrollmentOptions.Value.EnrollmentToken)
        && CryptographicallyEqual(presented, enrollmentOptions.Value.EnrollmentToken);

    private static bool CryptographicallyEqual(string a, string b)
    {
        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
