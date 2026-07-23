using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Agents.Grpc;
using Telechron.Host.Agents.Tests.Fixtures;
using Telechron.Sdk.Grpc;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Agents.Tests;

public sealed class AgentServiceImplTests : IAsyncLifetime
{
    private AgentServiceTestFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new AgentServiceTestFixture();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private static TestServerCallContext Context(CancellationToken ct = default) => new(ct);

    [Fact]
    public async Task RegisterAgent_WithInvalidEnrollmentToken_IsRejected()
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AgentServiceImpl>();

        var ex = await Assert.ThrowsAsync<global::Grpc.Core.RpcException>(() => service.RegisterAgent(
            new RegisterAgentRequest
            {
                EnrollmentToken = "wrong-token",
                MachineName = "test-machine",
                Hostname = "test-machine.local",
                MachineFingerprint = "fp-1",
            }, Context()));

        Assert.Equal(global::Grpc.Core.StatusCode.Unauthenticated, ex.StatusCode);
    }

    [Fact]
    public async Task RegisterAgent_WithValidToken_CreatesMachineAndSession()
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AgentServiceImpl>();

        var response = await service.RegisterAgent(new RegisterAgentRequest
        {
            EnrollmentToken = AgentServiceTestFixture.EnrollmentToken,
            MachineName = "test-machine",
            Hostname = "test-machine.local",
            MachineFingerprint = "fp-unique-1",
        }, Context());

        Assert.True(Guid.TryParse(response.MachineId, out _));
        Assert.False(string.IsNullOrEmpty(response.SessionToken));

        var machines = scope.ServiceProvider.GetRequiredService<IMachineRepository>();
        var machine = await machines.GetByFingerprintAsync("fp-unique-1");
        Assert.NotNull(machine);
        Assert.True(machine.IsOnline);
    }

    [Fact]
    public async Task RegisterAgent_SameFingerprintTwice_DedupsToSameMachine()
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AgentServiceImpl>();
        var request = new RegisterAgentRequest
        {
            EnrollmentToken = AgentServiceTestFixture.EnrollmentToken,
            MachineName = "test-machine",
            Hostname = "test-machine.local",
            MachineFingerprint = "fp-dedup-test",
        };

        var first = await service.RegisterAgent(request, Context());
        var second = await service.RegisterAgent(request, Context());

        Assert.Equal(first.MachineId, second.MachineId);

        var machines = scope.ServiceProvider.GetRequiredService<IMachineRepository>();
        var all = await machines.GetAllAsync();
        Assert.Single(all, m => m.MachineFingerprint == "fp-dedup-test");
    }

    [Fact]
    public async Task Heartbeat_WithValidSession_UpdatesLastHeartbeat()
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AgentServiceImpl>();
        var registration = await service.RegisterAgent(new RegisterAgentRequest
        {
            EnrollmentToken = AgentServiceTestFixture.EnrollmentToken,
            MachineName = "hb-machine",
            Hostname = "hb-machine.local",
            MachineFingerprint = "fp-heartbeat",
        }, Context());

        var response = await service.Heartbeat(new HeartbeatRequest
        {
            MachineId = registration.MachineId,
            SessionToken = registration.SessionToken,
        }, Context());

        Assert.True(response.Acknowledged);

        var machines = scope.ServiceProvider.GetRequiredService<IMachineRepository>();
        var machine = await machines.GetByIdAsync(Guid.Parse(registration.MachineId));
        Assert.NotNull(machine!.LastHeartbeatUtc);
    }

    [Fact]
    public async Task Heartbeat_WithInvalidSessionToken_IsRejected()
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AgentServiceImpl>();
        var registration = await service.RegisterAgent(new RegisterAgentRequest
        {
            EnrollmentToken = AgentServiceTestFixture.EnrollmentToken,
            MachineName = "bad-session-machine",
            Hostname = "bad-session-machine.local",
            MachineFingerprint = "fp-bad-session",
        }, Context());

        var ex = await Assert.ThrowsAsync<global::Grpc.Core.RpcException>(() => service.Heartbeat(
            new HeartbeatRequest { MachineId = registration.MachineId, SessionToken = "totally-wrong-token" },
            Context()));

        Assert.Equal(global::Grpc.Core.StatusCode.Unauthenticated, ex.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_WithMismatchedMachineId_IsRejected()
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AgentServiceImpl>();
        var registrationA = await service.RegisterAgent(new RegisterAgentRequest
        {
            EnrollmentToken = AgentServiceTestFixture.EnrollmentToken,
            MachineName = "machine-a",
            Hostname = "machine-a.local",
            MachineFingerprint = "fp-a",
        }, Context());
        var registrationB = await service.RegisterAgent(new RegisterAgentRequest
        {
            EnrollmentToken = AgentServiceTestFixture.EnrollmentToken,
            MachineName = "machine-b",
            Hostname = "machine-b.local",
            MachineFingerprint = "fp-b",
        }, Context());

        // Machine A's session token presented against Machine B's ID must fail.
        var ex = await Assert.ThrowsAsync<global::Grpc.Core.RpcException>(() => service.Heartbeat(
            new HeartbeatRequest { MachineId = registrationB.MachineId, SessionToken = registrationA.SessionToken },
            Context()));

        Assert.Equal(global::Grpc.Core.StatusCode.Unauthenticated, ex.StatusCode);
    }
}
