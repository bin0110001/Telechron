namespace Telechron.Sdk.Repair;

public enum RepairOutcomeStatus
{
    // Verify passed, policy allowed autonomous commit, and the commit landed.
    Committed,

    // Verify passed but the outcome is paused for a human decision --
    // either because Project policy is RequireApproval, or because a gate
    // (privileged-path, drift, oscillation, diff-scope, synthesis-needed)
    // forced RequireApproval regardless of policy.
    PendingApproval,

    // Verify failed (build/test/drift) -- the working tree was reverted
    // to the pre-attempt snapshot.
    Reverted,

    // No fix could be produced (no deterministic fix applied and the LLM
    // path declined, errored, or is out of budget) -- nothing was applied,
    // nothing to revert.
    NoFixProduced,

    // R-FIX3 governance short-circuited the attempt before Generate Fix
    // ever ran (attempt cap, cost cap, dedup hit, decline).
    GovernanceDeclined,
}

// The result of one pass through the single repair pipeline for a
// RepairRequest (R-NS2/R-FIX2). RepairAttemptId is always populated once
// Generate Fix has run, even for a Reverted outcome -- R-DM3a's provenance
// chain is meant to record failed attempts too, not just successes.
public sealed record RepairOutcome(
    RepairOutcomeStatus Status,
    Guid? RepairAttemptId,
    string Reason,
    PatchDiff? Patch,
    IReadOnlyList<string> ForcedApprovalReasons);
