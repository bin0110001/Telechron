using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Scheduling;

namespace Telechron.Host.Controllers;

public sealed record ScheduleResponse(
    Guid Id, Guid WorkflowId, Guid ProjectId, Guid? MachineId, string CronExpression,
    bool IsEnabled, bool SerializePerMachine, bool SerializePerProject, DateTimeOffset CreatedAtUtc, DateTimeOffset? LastFiredAtUtc);

// R-SCH1/R-UI2: durable schedule definitions, scoped by Project.
[ApiController]
[Route("api/projects/{projectId:guid}/schedules")]
[Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireViewer)]
public sealed class SchedulingController(ISchedulerService schedulerService, IProjectAccessChecker accessChecker) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScheduleResponse>>> ListAsync(Guid projectId, CancellationToken ct)
    {
        if (!await accessChecker.CanViewAsync(User, projectId, ct))
            return Forbid();

        var schedules = await schedulerService.GetSchedulesForProjectAsync(projectId, ct);
        return Ok(schedules.Select(ToResponse).ToList());
    }

    private static ScheduleResponse ToResponse(ScheduleDefinition s) =>
        new(s.Id, s.WorkflowId, s.ProjectId, s.MachineId, s.CronExpression, s.IsEnabled, s.SerializePerMachine, s.SerializePerProject, s.CreatedAtUtc, s.LastFiredAtUtc);
}
