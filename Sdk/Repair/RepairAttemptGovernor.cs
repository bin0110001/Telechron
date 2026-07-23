using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Sdk.Repair;

public sealed record RepairAttemptGovernorOptions(int MaxAttemptsPerFinding = 5);

// Default R-FIX3 implementation: attempt caps + cross-run dedup, both
// keyed off the same RepairAttempt history already persisted for R-DM3a
// provenance -- no separate counter store needed. Cost caps are
// deliberately NOT duplicated here: Phase 6's SpendCapEnforcer already
// gates every LlmDispatcher.DispatchAsync call (R-LLM4), and the LLM fix
// path routes through that dispatcher, so a repair attempt that reaches
// Generate Fix is already cost-capped at the LLM-call level. This governor
// only adds the repair-attempt-count dimension, which SpendCapEnforcer
// has no visibility into (a $0 deterministic-fix retry loop would sail
// through a cost cap forever).
public sealed class RepairAttemptGovernor(
    IRepairAttemptRepository repairAttemptRepository,
    RepairAttemptGovernorOptions options) : IRepairAttemptGovernor
{
    public RepairAttemptGovernor(IRepairAttemptRepository repairAttemptRepository)
        : this(repairAttemptRepository, new RepairAttemptGovernorOptions())
    {
    }

    public async Task<GovernanceCheckResult> CheckAsync(Guid projectId, IReadOnlyList<Finding> findings, CancellationToken ct = default)
    {
        foreach (var finding in findings)
        {
            var priorAttempts = await repairAttemptRepository.GetByFindingAsync(finding.Id, ct);
            if (priorAttempts.Count >= options.MaxAttemptsPerFinding)
            {
                return new GovernanceCheckResult(true,
                    $"Finding {finding.Id} has already accrued {priorAttempts.Count} repair attempts, at the {options.MaxAttemptsPerFinding}-attempt cap.");
            }
        }

        return new GovernanceCheckResult(false, null);
    }
}
