namespace Telechron.Host.Workflows;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Workflows;

public sealed class WorkflowRecoveryService(
    IWorkflowRunRepository runRepo,
    IWorkflowEngine workflowEngine,
    ILogger<WorkflowRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WorkflowRecoveryService starting recovery check...");

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
