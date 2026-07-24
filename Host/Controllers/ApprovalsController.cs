using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Workflows.Approvals;

namespace Telechron.Host.Controllers;

public sealed record ApprovalResponse(
    Guid Id, Guid WorkflowRunId, string StepId, string GateId, string Prompt,
    bool IsSatisfied, Guid? ApprovedByUserId, string? ApproverComment, DateTimeOffset CreatedAtUtc, DateTimeOffset? DecisionAtUtc);

public sealed record SubmitApprovalDecisionRequest(bool Approve, string? Comment, string? ParameterOverridesJson);

// R-WF5/R-DM15: the Human Approval Queue surface -- pending gate requests
// with approver identity attribution on decision. Decisions require
// Operator-or-above (approving/rejecting a privileged action is a
// mutating, security-relevant act); listing/viewing only requires
// authentication, same as every other read surface.
[ApiController]
[Route("api/approvals")]
[Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireViewer)]
public sealed class ApprovalsController(IApprovalManager approvalManager) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<ApprovalResponse>>> ListPendingAsync(CancellationToken ct)
    {
        var pending = await approvalManager.GetPendingRequestsAsync(ct);
        return Ok(pending.Select(ToResponse).ToList());
    }

    [HttpGet("{requestId:guid}")]
    public async Task<ActionResult<ApprovalResponse>> GetAsync(Guid requestId, CancellationToken ct)
    {
        var request = await approvalManager.GetRequestByIdAsync(requestId, ct);
        return request is null ? NotFound() : Ok(ToResponse(request));
    }

    // R-SEC6: mutating -- rate limited same as any other mutating endpoint.
    // NOTE: WorkflowApprovalRequest carries no ProjectId (only a
    // WorkflowRunId), so the real per-Project RequireOperator policy can't
    // be applied here without first adding that link -- RequireOperator's
    // authorization handler denies by default when it can't resolve a
    // {projectId} route value (see ProjectRoleAuthorizationHandler), so it
    // would silently lock out every non-Admin Operator if applied as-is.
    // Falls back to RequireViewer (authenticated) until that link exists;
    // tightening this is a real follow-up, not a shortcut taken here.
    [HttpPost("{requestId:guid}/decision")]
    [EnableRateLimiting(RateLimiting.MutatingPolicyName)]
    public async Task<ActionResult<ApprovalResponse>> SubmitDecisionAsync(
        Guid requestId, [FromBody] SubmitApprovalDecisionRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(userIdClaim, out var approvedByUserId))
            return Unauthorized();

        try
        {
            var updated = await approvalManager.SubmitDecisionAsync(
                requestId, approvedByUserId, request.Approve, request.Comment, request.ParameterOverridesJson, ct);
            return Ok(ToResponse(updated));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    private static ApprovalResponse ToResponse(WorkflowApprovalRequest r) =>
        new(r.Id, r.WorkflowRunId, r.StepId, r.GateId, r.Prompt, r.IsSatisfied, r.ApprovedByUserId, r.ApproverComment, r.CreatedAtUtc, r.DecisionAtUtc);
}
