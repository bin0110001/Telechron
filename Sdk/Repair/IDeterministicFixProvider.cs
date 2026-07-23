using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Repair;

public sealed record DeterministicFixResult(bool Handled, PatchDiff? Patch, string Description);

// R-FIX5: "Deterministic fixes execute before LLM-based fixes." A
// deterministic fix provider recognizes a narrow, mechanically-fixable
// Finding shape (e.g. a file-length lint violation with an obvious
// mechanical split, a known lint auto-fix) and produces a patch without
// ever calling an LLM. Handled=false means "not my Finding shape," not
// "I tried and failed" -- the orchestrator falls through to the LLM path
// on Handled=false, never on an exception.
public interface IDeterministicFixProvider
{
    Task<DeterministicFixResult> TryFixAsync(Finding finding, string projectRootPath, CancellationToken ct = default);
}
