using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Repair;

// R-FIX5's ordering seam: tries every registered deterministic fix
// provider in order before the pipeline ever falls through to the LLM
// path. No concrete provider is registered yet -- neither test-failure
// nor file-length-lint Findings have an honest mechanical fix (both
// require real understanding of intent, not a rote transformation), so
// this composite currently always returns Handled=false and every repair
// goes through Generate Fix's LLM path. Future Finding types with a truly
// mechanical fix (e.g. a known lint auto-fix) register a provider here
// without the orchestrator itself changing.
public sealed class CompositeDeterministicFixProvider(IReadOnlyList<IDeterministicFixProvider> providers) : IDeterministicFixProvider
{
    public CompositeDeterministicFixProvider() : this([])
    {
    }

    public async Task<DeterministicFixResult> TryFixAsync(Finding finding, string projectRootPath, CancellationToken ct = default)
    {
        foreach (var provider in providers)
        {
            var result = await provider.TryFixAsync(finding, projectRootPath, ct);
            if (result.Handled)
                return result;
        }

        return new DeterministicFixResult(false, null, "No deterministic fix provider handled this Finding.");
    }
}
