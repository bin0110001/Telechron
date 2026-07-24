using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Security.Audit;

namespace Telechron.Host.Controllers;

public sealed record AuditEventResponse(
    long Sequence, AuditEventKind Kind, DateTimeOffset OccurredAtUtc, Guid? ActorUserId, Guid? ProjectId, string DetailJson);

public sealed record AuditChainVerificationResponse(bool IsIntact, long? FirstTamperedSequence);

// R-SEC7: the audit log spans every Project and system-level event, so
// this is a global Admin surface, not Project-scoped like the others --
// a Viewer/Operator on one Project has no business reading another
// Project's audit trail via this endpoint.
[ApiController]
[Route("api/audit-log")]
[Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireAdmin)]
public sealed class AuditLogController(IAuditLog auditLog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditEventResponse>>> ListAsync(
        [FromQuery] long fromSequence, [FromQuery] int limit, CancellationToken ct)
    {
        var events = await auditLog.ReadAsync(fromSequence, limit > 0 ? Math.Min(limit, 500) : 100, ct);
        return Ok(events.Select(e => new AuditEventResponse(e.Sequence, e.Kind, e.OccurredAtUtc, e.ActorUserId, e.ProjectId, e.DetailJson)).ToList());
    }

    [HttpGet("verify")]
    public async Task<ActionResult<AuditChainVerificationResponse>> VerifyAsync(CancellationToken ct)
    {
        var result = await auditLog.VerifyChainAsync(ct);
        return Ok(new AuditChainVerificationResponse(result.IsIntact, result.FirstTamperedSequence));
    }
}
