using Microsoft.Extensions.Logging;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Retention;

// R-PER7: runs the retention pass on a fixed interval. Policies are
// deliberately conservative defaults for this scaffolding pass — tuning
// per-deployment retention is a later phase's UI/config surface (R-UI2
// Scheduling), this just proves the mechanism works end-to-end.
public sealed class ScheduledRetentionHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledRetentionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly RetentionPolicy RunPolicy = new(MaxAge: TimeSpan.FromDays(90), MaxCount: 10_000);
    private static readonly RetentionPolicy FindingPolicy = new(MaxAge: TimeSpan.FromDays(180), MaxCount: 50_000);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var pass = scope.ServiceProvider.GetRequiredService<RetentionPass>();
                await pass.RunRetentionAsync(RunPolicy, stoppingToken);
                await pass.FindingRetentionAsync(FindingPolicy, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Scheduled retention pass failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
