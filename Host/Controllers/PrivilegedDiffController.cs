using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Controllers;

public sealed record PendingRepairDiffResponse(Guid Id, IReadOnlyList<Guid> FindingIds, string SnapshotRef, string PatchDiff, DateTimeOffset CreatedAtUtc);

// R-SEC4/R-FIX12/R-FIX13: repair attempts still awaiting a human decision
// (ApprovalDecision == null) -- this is the actual real-world set a
// privileged-diff reviewer needs, regardless of which specific gate
// (privileged-path, diff-scope, drift, oscillation) forced the pause;
// RepairAttempt has no persisted "which gate fired" field to filter by
// more precisely than that.
[ApiController]
[Route("api/projects/{projectId:guid}/pending-repair-diffs")]
[Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireViewer)]
public sealed class PrivilegedDiffController(
    IRepairAttemptRepository repairAttemptRepository, IFindingRepository findingRepository, IProjectAccessChecker accessChecker) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PendingRepairDiffResponse>>> ListAsync(Guid projectId, CancellationToken ct)
    {
        if (!await accessChecker.CanViewAsync(User, projectId, ct))
            return Forbid();

        var projectFindingIds = (await findingRepository.GetByProjectAsync(projectId, ct))
            .Select(f => f.Id)
            .ToHashSet();

        var allAttempts = await repairAttemptRepository.GetAllAsync(ct);
        var pending = allAttempts
            .Where(a => a.ApprovalDecision is null && a.FindingIds.Any(projectFindingIds.Contains))
            .Select(a => new PendingRepairDiffResponse(a.Id, a.FindingIds, a.SnapshotRef, a.PatchDiff, a.CreatedAtUtc))
            .ToList();

        return Ok(pending);
    }
}
