namespace Telechron.Host.Scheduling.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Telechron.Host.Scheduling;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Scheduling;

public sealed class SchedulerServiceTests
{
    [Fact]
    public async Task TriggerScheduleAsync_SerializesPerProject_AndFiresWorkflow()
    {
        var engine = new FakeWorkflowEngine();
        var scheduler = new SchedulerService(engine, NullLogger<SchedulerService>.Instance);

        var projectId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();

        var schedule = new ScheduleDefinition
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            ProjectId = projectId,
            MachineId = null,
            CronExpression = "* * * * *",
            IsEnabled = true,
            SerializePerMachine = false,
            SerializePerProject = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastFiredAtUtc = null
        };

        await scheduler.CreateScheduleAsync(schedule);

        var run = await scheduler.TriggerScheduleAsync(schedule.Id);
        Assert.NotNull(run);
        Assert.Equal(workflowId, run.WorkflowId);

        var schedules = await scheduler.GetSchedulesForProjectAsync(projectId);
        Assert.Single(schedules);
        Assert.NotNull(schedules[0].LastFiredAtUtc);
    }

    private sealed class FakeWorkflowEngine : Telechron.Sdk.Workflows.IWorkflowEngine
    {
        public Task<WorkflowRun> StartWorkflowAsync(Guid workflowId, Dictionary<string, string>? inputVariables = null, CancellationToken ct = default) =>
            Task.FromResult(new WorkflowRun
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflowId,
                Status = WorkflowRunStatus.Passed,
                DefinitionSnapshotJson = "{}",
                StartedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow
            });

        public Task<WorkflowRun> ExecuteRunAsync(Guid workflowRunId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkflowRun> ResumeRunAsync(Guid workflowRunId, Guid approvalRequestId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WorkflowRun> CancelRunAsync(Guid workflowRunId, string reason, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
