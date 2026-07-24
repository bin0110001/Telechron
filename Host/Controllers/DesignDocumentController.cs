using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Controllers;

public sealed record RequirementResponse(
    Guid Id, string RequirementId, string Title, string Body, RequirementStatus Status, int CurrentRevisionNumber);

public sealed record DesignDocumentResponse(
    Guid Id, Guid ProjectId, string Title, DateTimeOffset CreatedAtUtc, IReadOnlyList<RequirementResponse> Requirements);

// R-DM16/R-UI2: the Design Document surface -- a Project's living
// requirements. Read-only for now (propose/approve edit diff routes
// through DesignDocumentManager's privileged-path gate, R-DM16b, which
// is a separate, larger surface than this session scoped in).
[ApiController]
[Route("api/projects/{projectId:guid}/design-document")]
[Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireViewer)]
public sealed class DesignDocumentController(
    IDesignDocumentRepository designDocRepository, IRequirementRepository requirementRepository, IProjectAccessChecker accessChecker) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DesignDocumentResponse>> GetAsync(Guid projectId, CancellationToken ct)
    {
        if (!await accessChecker.CanViewAsync(User, projectId, ct))
            return Forbid();

        var designDoc = await designDocRepository.GetByProjectAsync(projectId, ct);
        if (designDoc is null)
            return NotFound();

        var requirements = await requirementRepository.GetByDesignDocumentAsync(designDoc.Id, ct);

        return Ok(new DesignDocumentResponse(
            designDoc.Id, designDoc.ProjectId, designDoc.Title, designDoc.CreatedAtUtc,
            requirements.Select(r => new RequirementResponse(r.Id, r.RequirementId, r.Title, r.Body, r.Status, r.CurrentRevisionNumber)).ToList()));
    }
}
