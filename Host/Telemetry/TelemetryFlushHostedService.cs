namespace Telechron.Host.Telemetry;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telechron.Sdk.Telemetry;

// R-PER5: periodic background flush -- without this, events enqueued via
// ITelemetryBatcher.EnqueueEvent only ever drain when something happens to
// call FlushAsync directly, which nothing in the Host does.
public sealed class TelemetryFlushHostedService(
    ITelemetryBatcher telemetryBatcher,
    ILogger<TelemetryFlushHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            bool tickedBeforeCancellation;
            try
            {
                tickedBeforeCancellation = await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!tickedBeforeCancellation)
                break;

            try
            {
                var flushed = await telemetryBatcher.FlushAsync(stoppingToken);
                if (flushed > 0)
                    logger.LogDebug("Flushed {Count} telemetry events.", flushed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error flushing telemetry batch.");
            }
        }
    }
}
