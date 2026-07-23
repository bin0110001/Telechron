using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telechron.Host.Agents.Grpc;
using Telechron.Host.Agents.Tests.Fixtures;
using Telechron.Host.Agents.Watchdog;
using Telechron.Sdk.Domain;
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

    [Fact]
    public async Task Heartbeat_WithActiveRunId_UpdatesRunLastHeartbeat()
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AgentServiceImpl>();
        var registration = await service.RegisterAgent(new RegisterAgentRequest
        {
            EnrollmentToken = AgentServiceTestFixture.EnrollmentToken,
            MachineName = "run-hb-machine",
            Hostname = "run-hb-machine.local",
            MachineFingerprint = "fp-run-heartbeat",
        }, Context());
        var machineId = Guid.Parse(registration.MachineId);

        var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var run = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = await scope.SeedProjectAsync(),
            MachineId = machineId,
            Status = RunStatus.Running,
            StartedAtUtc = DateTimeOffset.UtcNow,
        };
        await runs.AddAsync(run);

        var response = await service.Heartbeat(new HeartbeatRequest
        {
            MachineId = registration.MachineId,
            SessionToken = registration.SessionToken,
            ActiveRunIds = { run.Id.ToString() },
        }, Context());

        Assert.True(response.Acknowledged);
        var updated = await runs.GetByIdAsync(run.Id);
        Assert.NotNull(updated!.LastHeartbeatUtc);
        Assert.Equal(RunStatus.Running, updated.Status);
    }

    [Fact]
    public async Task Heartbeat_WithActiveRunIdOnStalledRun_ResumesToRunning()
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AgentServiceImpl>();
        var registration = await service.RegisterAgent(new RegisterAgentRequest
        {
            EnrollmentToken = AgentServiceTestFixture.EnrollmentToken,
            MachineName = "resume-machine",
            Hostname = "resume-machine.local",
            MachineFingerprint = "fp-resume",
        }, Context());
        var machineId = Guid.Parse(registration.MachineId);

        var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var run = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = await scope.SeedProjectAsync(),
            MachineId = machineId,
            Status = RunStatus.Stalled,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        await runs.AddAsync(run);

        await service.Heartbeat(new HeartbeatRequest
        {
            MachineId = registration.MachineId,
            SessionToken = registration.SessionToken,
            ActiveRunIds = { run.Id.ToString() },
        }, Context());

        var updated = await runs.GetByIdAsync(run.Id);
        Assert.Equal(RunStatus.Running, updated!.Status);
    }

    [Fact]
    public async Task Heartbeat_WithActiveRunIdOwnedByAnotherMachine_IsIgnored()
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AgentServiceImpl>();
        var registrationA = await service.RegisterAgent(new RegisterAgentRequest
        {
            EnrollmentToken = AgentServiceTestFixture.EnrollmentToken,
            MachineName = "owner-machine",
            Hostname = "owner-machine.local",
            MachineFingerprint = "fp-owner",
        }, Context());
        var registrationB = await service.RegisterAgent(new RegisterAgentRequest
        {
            EnrollmentToken = AgentServiceTestFixture.EnrollmentToken,
            MachineName = "impersonator-machine",
            Hostname = "impersonator-machine.local",
            MachineFingerprint = "fp-impersonator",
        }, Context());

        var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var run = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = await scope.SeedProjectAsync(),
            MachineId = Guid.Parse(registrationA.MachineId),
            Status = RunStatus.Stalled,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        await runs.AddAsync(run);

        // Machine B claims Machine A's Run in its heartbeat -- must not resume it.
        await service.Heartbeat(new HeartbeatRequest
        {
            MachineId = registrationB.MachineId,
            SessionToken = registrationB.SessionToken,
            ActiveRunIds = { run.Id.ToString() },
        }, Context());

        var unchanged = await runs.GetByIdAsync(run.Id);
        Assert.Equal(RunStatus.Stalled, unchanged!.Status);
        Assert.Null(unchanged.LastHeartbeatUtc);
    }

    // Phase 4 exit criteria, end to end: "disconnect->reconnect resumes
    // within the grace window." Exercises both halves of R-SCH5 together --
    // StalledRunWatchdogPass marking a Run Stalled after a missed heartbeat,
    // then AgentServiceImpl.Heartbeat resuming it on the next heartbeat --
    // rather than each in isolation as the other tests in this file and in
    // StalledRunWatchdogPassTests.cs do.
    [Fact]
    public async Task DisconnectThenReconnectWithinGraceWindow_RunResumesRatherThanRestarting()
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AgentServiceImpl>();
        var registration = await service.RegisterAgent(new RegisterAgentRequest
        {
            EnrollmentToken = AgentServiceTestFixture.EnrollmentToken,
            MachineName = "e2e-machine",
            Hostname = "e2e-machine.local",
            MachineFingerprint = "fp-e2e-reconnect",
        }, Context());
        var machineId = Guid.Parse(registration.MachineId);

        var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var run = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = await scope.SeedProjectAsync(),
            MachineId = machineId,
            Status = RunStatus.Running,
            // Simulates an Agent that connected, ran a while, then stopped
            // heartbeating (network blip / process restart) well past any
            // reasonable grace window.
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-8),
        };
        await runs.AddAsync(run);

        var watchdog = new StalledRunWatchdogPass(
            runs, Options.Create(new StalledRunWatchdogOptions { GraceWindow = TimeSpan.FromMinutes(2) }),
            NullLogger<StalledRunWatchdogPass>.Instance);
        var stalledCount = await watchdog.ScanAsync();
        Assert.Equal(1, stalledCount);
        Assert.Equal(RunStatus.Stalled, (await runs.GetByIdAsync(run.Id))!.Status);

        // The Agent reconnects and heartbeats the same Run within what a
        // real deployment would configure as the grace window -- this must
        // resume it, not require the Run to be restarted from scratch.
        await service.Heartbeat(new HeartbeatRequest
        {
            MachineId = registration.MachineId,
            SessionToken = registration.SessionToken,
            ActiveRunIds = { run.Id.ToString() },
        }, Context());

        var resumed = await runs.GetByIdAsync(run.Id);
        Assert.Equal(RunStatus.Running, resumed!.Status);
        Assert.NotNull(resumed.LastHeartbeatUtc);

        // A subsequent watchdog scan (using the same grace window as if no
        // time had passed since the just-sent heartbeat) must NOT re-stall
        // it -- the resumed Run's heartbeat clock has been reset, not
        // merely its status flipped back.
        var secondScanStalledCount = await watchdog.ScanAsync();
        Assert.Equal(0, secondScanStalledCount);
        Assert.Equal(RunStatus.Running, (await runs.GetByIdAsync(run.Id))!.Status);
    }
}
