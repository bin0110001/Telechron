using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Controllers;

public sealed record WorkflowResponse(
    Guid Id, Guid ProjectId, string Name, string DefinitionJson, WorkflowFailurePolicy FailurePolicy, DateTimeOffset CreatedAtUtc);

// R-WF1/R-DM5/R-UI2: Workflow definitions, scoped by Project. DefinitionJson
// is returned as-is (the frontend's WorkflowDefinition/WorkflowStepDefinition
// shape already matches Sdk/Workflows/WorkflowDefinition.cs's JSON) so the
// graph editor can render real nodes/edges from DependsOnStepIds instead of
// a fixed illustration.
[ApiController]
[Route("api/projects/{projectId:guid}/workflows")]
[Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireViewer)]
public sealed class WorkflowsController(IWorkflowRepository workflowRepository, IProjectAccessChecker accessChecker) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkflowResponse>>> ListAsync(Guid projectId, CancellationToken ct)
    {
        if (!await accessChecker.CanViewAsync(User, projectId, ct))
            return Forbid();

        var workflows = await workflowRepository.GetByProjectAsync(projectId, ct);
        return Ok(workflows.Select(ToResponse).ToList());
    }

    [HttpGet("{workflowId:guid}")]
    public async Task<ActionResult<WorkflowResponse>> GetAsync(Guid projectId, Guid workflowId, CancellationToken ct)
    {
        if (!await accessChecker.CanViewAsync(User, projectId, ct))
            return Forbid();

        var workflow = await workflowRepository.GetByIdAsync(workflowId, ct);
        if (workflow is null || workflow.ProjectId != projectId)
            return NotFound();

        return Ok(ToResponse(workflow));
    }

    private static WorkflowResponse ToResponse(Workflow w) =>
        new(w.Id, w.ProjectId, w.Name, w.DefinitionJson, w.FailurePolicy, w.CreatedAtUtc);
}
