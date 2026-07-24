using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Controllers;

public sealed record ProjectResponse(
    Guid Id, string Name, string RootPath, RepairPolicy RepairPolicy,
    Guid? ToolchainId, Guid? LlmConnectionId, DateTimeOffset CreatedAtUtc);

// R-UI2: the Projects surface. A global Admin sees every Project; anyone
// else sees only Projects they own or hold a ProjectMembership on (the
// same per-Project role claims JwtTokenService already issues).
[ApiController]
[Route("api/projects")]
[Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireViewer)]
public sealed class ProjectsController(IProjectRepository projectRepository, IProjectAccessChecker accessChecker) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> ListAsync(CancellationToken ct)
    {
        var allProjects = await projectRepository.GetAllAsync(ct);
        var visible = new List<Project>();
        foreach (var project in allProjects)
        {
            if (await accessChecker.CanViewAsync(User, project.Id, ct))
                visible.Add(project);
        }

        return Ok(visible.Select(ToResponse).ToList());
    }

    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult<ProjectResponse>> GetAsync(Guid projectId, CancellationToken ct)
    {
        var project = await projectRepository.GetByIdAsync(projectId, ct);
        if (project is null)
            return NotFound();

        if (!await accessChecker.CanViewAsync(User, projectId, ct))
            return Forbid();

        return Ok(ToResponse(project));
    }

    private static ProjectResponse ToResponse(Project p) =>
        new(p.Id, p.Name, p.RootPath, p.RepairPolicy, p.ToolchainId, p.LlmConnectionId, p.CreatedAtUtc);
}
