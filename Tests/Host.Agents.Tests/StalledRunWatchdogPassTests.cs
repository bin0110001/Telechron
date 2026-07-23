using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telechron.Host.Agents.Tests.Fixtures;
using Telechron.Host.Agents.Watchdog;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Agents.Tests;

public sealed class StalledRunWatchdogPassTests : IAsyncLifetime
{
    private AgentServiceTestFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new AgentServiceTestFixture();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private static StalledRunWatchdogPass CreatePass(IRunRepository runs, TimeSpan graceWindow) =>
        new(runs, Options.Create(new StalledRunWatchdogOptions { GraceWindow = graceWindow }), NullLogger<StalledRunWatchdogPass>.Instance);

    [Fact]
    public async Task ScanAsync_RunningWithStaleHeartbeat_IsMarkedStalled()
    {
        using var scope = _fixture.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var run = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = await scope.SeedProjectAsync(),
            Status = RunStatus.Running,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        await runs.AddAsync(run);

        var pass = CreatePass(runs, graceWindow: TimeSpan.FromMinutes(2));
        var stalledCount = await pass.ScanAsync();

        Assert.Equal(1, stalledCount);
        var updated = await runs.GetByIdAsync(run.Id);
        Assert.Equal(RunStatus.Stalled, updated!.Status);
    }

    [Fact]
    public async Task ScanAsync_RunningWithRecentHeartbeat_IsNotStalled()
    {
        using var scope = _fixture.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var run = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = await scope.SeedProjectAsync(),
            Status = RunStatus.Running,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            LastHeartbeatUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
        };
        await runs.AddAsync(run);

        var pass = CreatePass(runs, graceWindow: TimeSpan.FromMinutes(2));
        var stalledCount = await pass.ScanAsync();

        Assert.Equal(0, stalledCount);
        var updated = await runs.GetByIdAsync(run.Id);
        Assert.Equal(RunStatus.Running, updated!.Status);
    }

    [Fact]
    public async Task ScanAsync_RunningWithNoHeartbeatYet_GracedFromStartedAt()
    {
        using var scope = _fixture.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var projectId = await scope.SeedProjectAsync();
        var freshRun = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Status = RunStatus.Running,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            LastHeartbeatUtc = null,
        };
        var staleRun = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Status = RunStatus.Running,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastHeartbeatUtc = null,
        };
        await runs.AddAsync(freshRun);
        await runs.AddAsync(staleRun);

        var pass = CreatePass(runs, graceWindow: TimeSpan.FromMinutes(2));
        var stalledCount = await pass.ScanAsync();

        Assert.Equal(1, stalledCount);
        Assert.Equal(RunStatus.Running, (await runs.GetByIdAsync(freshRun.Id))!.Status);
        Assert.Equal(RunStatus.Stalled, (await runs.GetByIdAsync(staleRun.Id))!.Status);
    }

    [Fact]
    public async Task ScanAsync_PendingRun_IsNeverStalled()
    {
        using var scope = _fixture.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var run = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = await scope.SeedProjectAsync(),
            Status = RunStatus.Pending,
        };
        await runs.AddAsync(run);

        var pass = CreatePass(runs, graceWindow: TimeSpan.FromMinutes(2));
        var stalledCount = await pass.ScanAsync();

        Assert.Equal(0, stalledCount);
        Assert.Equal(RunStatus.Pending, (await runs.GetByIdAsync(run.Id))!.Status);
    }
}
