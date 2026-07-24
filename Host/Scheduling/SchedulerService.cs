namespace Telechron.Host.Scheduling;

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Scheduling;
using Telechron.Sdk.Workflows;

// SchedulerService is a singleton (it's also the IHostedService running the
// background poll loop, and its in-memory schedule/lock state must be
// shared with the ISchedulerService callers) -- but IWorkflowEngine is
// scoped (DbContext-backed), so it's resolved from a fresh scope per call
// rather than injected directly (which DI would reject as a captive
// dependency).
public sealed class SchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<SchedulerService> logger) : BackgroundService, ISchedulerService
{
    private readonly ConcurrentDictionary<Guid, ScheduleDefinition> _schedules = new();
    private readonly ConcurrentDictionary<Guid, bool> _activeMachineLocks = new();
    private readonly ConcurrentDictionary<Guid, bool> _activeProjectLocks = new();

    public Task<ScheduleDefinition> CreateScheduleAsync(ScheduleDefinition schedule, CancellationToken ct = default)
    {
        _schedules[schedule.Id] = schedule;
        logger.LogInformation("Registered Schedule '{ScheduleId}' for Workflow '{WorkflowId}'.", schedule.Id, schedule.WorkflowId);
        return Task.FromResult(schedule);
    }

    public Task<IReadOnlyList<ScheduleDefinition>> GetSchedulesForProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var list = _schedules.Values.Where(s => s.ProjectId == projectId).ToList();
        return Task.FromResult<IReadOnlyList<ScheduleDefinition>>(list);
    }

    public async Task<WorkflowRun?> TriggerScheduleAsync(Guid scheduleId, CancellationToken ct = default)
    {
        if (!_schedules.TryGetValue(scheduleId, out var schedule) || !schedule.IsEnabled)
        {
            return null;
        }

        // Serialization checks
        if (schedule.SerializePerProject && _activeProjectLocks.ContainsKey(schedule.ProjectId))
        {
            logger.LogWarning("Execution for Project '{ProjectId}' is currently active; skipping scheduled run.", schedule.ProjectId);
            return null;
        }

        if (schedule.SerializePerMachine && schedule.MachineId.HasValue && _activeMachineLocks.ContainsKey(schedule.MachineId.Value))
        {
            logger.LogWarning("Execution for Machine '{MachineId}' is currently active; skipping scheduled run.", schedule.MachineId);
            return null;
        }

        try
        {
            if (schedule.SerializePerProject) _activeProjectLocks[schedule.ProjectId] = true;
            if (schedule.SerializePerMachine && schedule.MachineId.HasValue) _activeMachineLocks[schedule.MachineId.Value] = true;

            using var scope = scopeFactory.CreateScope();
            var workflowEngine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
            var run = await workflowEngine.StartWorkflowAsync(schedule.WorkflowId, ct: ct);

            _schedules[scheduleId] = schedule with { LastFiredAtUtc = DateTimeOffset.UtcNow };
            return run;
        }
        finally
        {
            if (schedule.SerializePerProject) _activeProjectLocks.TryRemove(schedule.ProjectId, out _);
            if (schedule.SerializePerMachine && schedule.MachineId.HasValue) _activeMachineLocks.TryRemove(schedule.MachineId.Value, out _);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SchedulerService started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                foreach (var schedule in _schedules.Values.Where(s => s.IsEnabled))
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    // Evaluate due schedules (e.g. fired > 1 min ago or first time)
                    if (!schedule.LastFiredAtUtc.HasValue || (now - schedule.LastFiredAtUtc.Value).TotalMinutes >= 1)
                    {
                        await TriggerScheduleAsync(schedule.Id, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in SchedulerService background loop.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
