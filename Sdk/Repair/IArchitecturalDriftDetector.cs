using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Repair;

public sealed record DriftCheckResult(bool IsDrift, string? Reason);

// R-FIX13: the generated patch is evaluated against the Requirement
// entries it touches or is tagged against. A patch that satisfies its
// Finding but contradicts an Active Requirement is flagged as drift
// rather than committed/hot-reloaded silently. Runs only when there are
// Active Requirements to check against -- an empty ActiveRequirements list
// (e.g. no Design Document yet) means nothing to drift from, not a
// vacuous pass that skips checking on a technicality worth flagging.
public interface IArchitecturalDriftDetector
{
    Task<DriftCheckResult> CheckAsync(PatchDiff patch, IReadOnlyList<Requirement> activeRequirements, CancellationToken ct = default);
}
