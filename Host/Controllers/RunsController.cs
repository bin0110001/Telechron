using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Controllers;

public sealed record RunResponse(
    Guid Id, Guid ProjectId, Guid? MachineId, RunStatus Status,
    DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc, DateTimeOffset? LastHeartbeatUtc);

// R-UI2: the Runs / Work Queue surface. Scoped by projectId query param
// rather than a global list, since Runs belong to a Project and R-SEC6
// project-scoped visibility applies the same as ProjectsController.
[ApiController]
[Route("api/runs")]
[Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireViewer)]
public sealed class RunsController(IRunRepository runRepository, IProjectAccessChecker accessChecker) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RunResponse>>> ListAsync([FromQuery] Guid projectId, CancellationToken ct)
    {
        if (projectId == Guid.Empty)
            return BadRequest("projectId query parameter is required.");

        if (!await accessChecker.CanViewAsync(User, projectId, ct))
            return Forbid();

        var runs = await runRepository.GetByProjectAsync(projectId, ct);
        return Ok(runs.Select(ToResponse).ToList());
    }

    [HttpGet("{runId:guid}")]
    public async Task<ActionResult<RunResponse>> GetAsync(Guid runId, CancellationToken ct)
    {
        var run = await runRepository.GetByIdAsync(runId, ct);
        if (run is null)
            return NotFound();

        if (!await accessChecker.CanViewAsync(User, run.ProjectId, ct))
            return Forbid();

        return Ok(ToResponse(run));
    }

    private static RunResponse ToResponse(Run r) =>
        new(r.Id, r.ProjectId, r.MachineId, r.Status, r.StartedAtUtc, r.CompletedAtUtc, r.LastHeartbeatUtc);
}
