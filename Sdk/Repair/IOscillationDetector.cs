namespace Telechron.Sdk.Repair;

public sealed record OscillationCheckResult(bool IsOscillation, string? Reason);

// R-FIX11: beyond attempt caps, the pipeline keeps a per-file patch-diff
// signature history. If a newly generated fix's net diff matches (or
// closely matches) a prior reverted or superseded patch within the same
// repair lineage, this short-circuits as an oscillation. "Lineage" is
// keyed by the Finding set being repaired -- history from an unrelated
// Finding's repairs is irrelevant.
public interface IOscillationDetector
{
    OscillationCheckResult Check(PatchDiff patch, IReadOnlyList<string> priorPatchSignatures);

    // A signature stable across whitespace-only diff noise but sensitive
    // to actual content changes -- computed once per patch, stored on the
    // RepairAttempt for the next attempt in the same lineage to compare against.
    string ComputeSignature(PatchDiff patch);
}
