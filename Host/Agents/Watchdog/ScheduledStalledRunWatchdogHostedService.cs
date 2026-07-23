using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Telechron.Host.Agents.Watchdog;

public sealed class ScheduledStalledRunWatchdogHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<StalledRunWatchdogOptions> options,
    ILogger<ScheduledStalledRunWatchdogHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.ScanInterval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var pass = scope.ServiceProvider.GetRequiredService<StalledRunWatchdogPass>();
                await pass.ScanAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Stalled-run watchdog scan failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
