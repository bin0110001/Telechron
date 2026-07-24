using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Controllers;

// R-DM10: connections are reusable across Projects, not owned by one --
// visible to any authenticated user, same reasoning as Machines/Modules.
// SecretHandle is included as a handle only (R-SEC1), never resolved.
public sealed record LlmConnectionResponse(Guid Id, string Name, string Provider, string? SecretHandle, DateTimeOffset CreatedAtUtc);

public sealed record LlmCallResponse(
    Guid Id, Guid LlmConnectionId, Guid? ProjectId, string Provider, string Model,
    int PromptTokens, int CompletionTokens, decimal EstimatedCostUsd, bool Succeeded, DateTimeOffset OccurredAtUtc);

[ApiController]
[Route("api/llm")]
[Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireViewer)]
public sealed class LlmController(
    ILlmConnectionRepository connectionRepository, ILlmCallRepository callRepository, IProjectAccessChecker accessChecker) : ControllerBase
{
    [HttpGet("connections")]
    public async Task<ActionResult<IReadOnlyList<LlmConnectionResponse>>> ListConnectionsAsync(CancellationToken ct)
    {
        var connections = await connectionRepository.GetAllAsync(ct);
        return Ok(connections.Select(ToConnectionResponse).ToList());
    }

    // R-LLM3/R-LLM4: cost/spend dashboard -- scoped to a Project's calls
    // over a rolling window (default last 30 days), same access check as
    // every other Project-scoped surface.
    [HttpGet("calls")]
    public async Task<ActionResult<IReadOnlyList<LlmCallResponse>>> ListCallsAsync(
        [FromQuery] Guid projectId, [FromQuery] int lookbackDays, CancellationToken ct)
    {
        if (projectId == Guid.Empty)
            return BadRequest("projectId query parameter is required.");
        if (!await accessChecker.CanViewAsync(User, projectId, ct))
            return Forbid();

        var since = DateTimeOffset.UtcNow.AddDays(lookbackDays > 0 ? -lookbackDays : -30);
        var calls = await callRepository.GetSinceAsync(since, projectId, ct);
        return Ok(calls.Select(ToCallResponse).ToList());
    }

    private static LlmConnectionResponse ToConnectionResponse(LlmConnection c) =>
        new(c.Id, c.Name, c.Provider, c.SecretHandle, c.CreatedAtUtc);

    private static LlmCallResponse ToCallResponse(LlmCall c) =>
        new(c.Id, c.LlmConnectionId, c.ProjectId, c.Provider, c.Model, c.PromptTokens, c.CompletionTokens, c.EstimatedCostUsd, c.Succeeded, c.OccurredAtUtc);
}
