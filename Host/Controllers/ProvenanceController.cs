using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Controllers;

public sealed record RepairAttemptResponse(
    Guid Id, IReadOnlyList<Guid> FindingIds, string SnapshotRef, RepairApprovalDecision? ApprovalDecision,
    Guid? ApproverUserId, string? CommitReference, string? ProvenanceRecordJson, DateTimeOffset CreatedAtUtc);

// R-SEC3/R-DM3a: "why did this change?" -- signed provenance records for
// repair attempts, scoped to a Project via its Findings (RepairAttempt
// itself has no direct ProjectId, only FindingIds -- same
// materialize-then-filter pattern Host/Persistence/Retention/RetentionPass
// already uses for a comparable relational gap).
[ApiController]
[Route("api/projects/{projectId:guid}/repair-attempts")]
[Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireViewer)]
public sealed class ProvenanceController(
    IRepairAttemptRepository repairAttemptRepository, IFindingRepository findingRepository, IProjectAccessChecker accessChecker) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RepairAttemptResponse>>> ListAsync(Guid projectId, CancellationToken ct)
    {
        if (!await accessChecker.CanViewAsync(User, projectId, ct))
            return Forbid();

        var projectFindingIds = (await findingRepository.GetByProjectAsync(projectId, ct))
            .Select(f => f.Id)
            .ToHashSet();

        var allAttempts = await repairAttemptRepository.GetAllAsync(ct);
        var scoped = allAttempts.Where(a => a.FindingIds.Any(projectFindingIds.Contains)).ToList();

        return Ok(scoped.Select(ToResponse).ToList());
    }

    private static RepairAttemptResponse ToResponse(RepairAttempt a) =>
        new(a.Id, a.FindingIds, a.SnapshotRef, a.ApprovalDecision, a.ApproverUserId, a.CommitReference, a.ProvenanceRecordJson, a.CreatedAtUtc);
}
