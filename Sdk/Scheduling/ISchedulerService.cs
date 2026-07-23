namespace Telechron.Sdk.Scheduling;

using Telechron.Sdk.Domain;

public interface ISchedulerService
{
    Task<ScheduleDefinition> CreateScheduleAsync(ScheduleDefinition schedule, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduleDefinition>> GetSchedulesForProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<WorkflowRun?> TriggerScheduleAsync(Guid scheduleId, CancellationToken ct = default);
}
