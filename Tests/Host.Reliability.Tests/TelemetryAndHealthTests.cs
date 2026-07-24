namespace Telechron.Host.Reliability.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Host.Persistence.Tests.Phase3;
using Telechron.Host.Reliability;
using Telechron.Host.Telemetry;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Telemetry;

public sealed class TelemetryAndHealthTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task TelemetryBatcher_BatchesAndFlushesEventsInCoreMemory()
    {
        var batcher = new TelemetryBatcher(NullLogger<TelemetryBatcher>.Instance);

        batcher.EnqueueEvent("RunStarted", "Audit", CorrelationContext.TraceId, "{}");
        batcher.EnqueueEvent("RunCompleted", "Audit", CorrelationContext.TraceId, "{}");

        Assert.Equal(2, batcher.PendingCount);

        var flushedCount = await batcher.FlushAsync();
        Assert.Equal(2, flushedCount);
        Assert.Equal(0, batcher.PendingCount);
    }

    // R-REL4: proves the monitor reads real repository state -- an empty
    // DB reports zero active agents, not a fabricated "10" (the bug this
    // replaces).
    [Fact]
    public async Task HostScalingMonitor_NoActiveAgents_ReportsZero()
    {
        using var scope = _db.CreateScope();
        var monitor = new HostScalingMonitor(
            scope.ServiceProvider.GetRequiredService<IAgentSessionRepository>(),
            scope.ServiceProvider.GetRequiredService<IWorkflowRunRepository>());

        var status = await monitor.EvaluateScalingStatusAsync();

        Assert.NotNull(status);
        Assert.Equal(0, status.ActiveAgentsCount);
        Assert.False(status.NearingCeiling);
    }
}
