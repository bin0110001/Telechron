namespace Telechron.Host.Workflows;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Workflows;

// R-WF4: hosted services are singletons, but IWorkflowRunRepository/
// IWorkflowEngine are scoped (DbContext-backed) -- resolves them from a
// fresh scope per recovery pass rather than injecting them directly,
// which the DI container would reject at startup (captive dependency).
public sealed class WorkflowRecoveryService(
    IServiceScopeFactory scopeFactory,
    ILogger<WorkflowRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WorkflowRecoveryService starting recovery check...");

        using var scope = scopeFactory.CreateScope();
        var runRepo = scope.ServiceProvider.GetRequiredService<IWorkflowRunRepository>();
        var workflowEngine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();

        try
        {
            var activeRuns = await runRepo.GetActiveAsync(stoppingToken);
            logger.LogInformation("Found {Count} active or awaiting-approval WorkflowRun(s) to recover.", activeRuns.Count);

            foreach (var run in activeRuns)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    logger.LogInformation("Recovering WorkflowRun '{RunId}' (Status: {Status})...", run.Id, run.Status);
                    await workflowEngine.ExecuteRunAsync(run.Id, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to recover WorkflowRun '{RunId}'.", run.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WorkflowRecoveryService encountered an error during startup recovery.");
        }
    }
}
