namespace Telechron.Host.Reliability.Tests;

using Telechron.Host.Reliability;
using Telechron.Host.Telemetry;
using Telechron.Sdk.Telemetry;

public sealed class TelemetryAndHealthTests
{
    [Fact]
    public async Task TelemetryBatcher_BatchesAndFlushesEventsInCoreMemory()
    {
        var batcher = new TelemetryBatcher();

        batcher.EnqueueEvent("RunStarted", "Audit", CorrelationContext.TraceId, "{}");
        batcher.EnqueueEvent("RunCompleted", "Audit", CorrelationContext.TraceId, "{}");

        Assert.Equal(2, batcher.PendingCount);

        var flushedCount = await batcher.FlushAsync();
        Assert.Equal(2, flushedCount);
        Assert.Equal(0, batcher.PendingCount);
    }

    [Fact]
    public async Task HostScalingMonitor_EvaluatesStatus()
    {
        var monitor = new HostScalingMonitor();
        var status = await monitor.EvaluateScalingStatusAsync();

        Assert.NotNull(status);
        Assert.True(status.ActiveAgentsCount > 0);
    }
}
