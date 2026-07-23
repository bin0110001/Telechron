using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Repair;

public sealed record GovernanceCheckResult(bool Declined, string? Reason);

// R-FIX3: repair is bounded, cost-safe, and governed by policy. Combines
// attempt caps, cost caps, decline short-circuiting, and cross-run
// dedup into one gate checked BEFORE Generate Fix ever runs (a declined
// attempt never reaches the LLM, so it never spends anything). Distinct
// from Phase 6's SpendCapEnforcer, which caps aggregate LLM spend across
// the whole system/project -- this caps repair ATTEMPTS specifically,
// even ones that would use $0 of LLM budget (e.g. a deterministic fix
// endlessly retried).
// R-FIX6: bounded rescanning shares this same attempt-cap accounting --
// a rescan cycle's Findings are checked through CheckAsync exactly like
// any other repair attempt, so cascading rescans can't spiral
// independently of the ordinary cap. There is no separate "record"
// method: accounting is self-maintaining because CheckAsync counts
// persisted RepairAttempt rows, and the orchestrator always persists one
// per attempt (including reverted/no-fix outcomes) before returning.
public interface IRepairAttemptGovernor
{
    Task<GovernanceCheckResult> CheckAsync(
        Guid projectId, IReadOnlyList<Finding> findings, CancellationToken ct = default);
}
