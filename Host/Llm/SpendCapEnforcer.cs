using Microsoft.Extensions.Options;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Llm;

public sealed class SpendCapEnforcer(ILlmCallRepository llmCallRepository, IOptions<SpendCapOptions> options) : ISpendCapEnforcer
{
    public async Task<SpendCapCheckResult> CheckAsync(Guid? projectId, CancellationToken ct = default)
    {
        var opts = options.Value;
        var windowStart = DateTimeOffset.UtcNow - opts.WindowDuration;

        // R-LLM4: "independent of per-repair/per-persona caps" -- global
        // spend is evaluated across ALL calls in the window regardless of
        // Project, and (if the call is Project-scoped) that Project's own
        // cap is evaluated independently on top. Both must pass.
        var globalSpend = (await llmCallRepository.GetSinceAsync(windowStart, projectId: null, ct)).Sum(c => c.EstimatedCostUsd);
        if (globalSpend >= opts.GlobalCapUsd)
        {
            return new SpendCapCheckResult(
                false, $"Global spend cap exceeded: ${globalSpend:F2} >= ${opts.GlobalCapUsd:F2} over the last {opts.WindowDuration}.",
                globalSpend, opts.GlobalCapUsd);
        }

        if (projectId is { } id && opts.PerProjectCapsUsd.TryGetValue(id, out var projectCap))
        {
            var projectSpend = (await llmCallRepository.GetSinceAsync(windowStart, id, ct)).Sum(c => c.EstimatedCostUsd);
            if (projectSpend >= projectCap)
            {
                return new SpendCapCheckResult(
                    false, $"Project spend cap exceeded: ${projectSpend:F2} >= ${projectCap:F2} over the last {opts.WindowDuration}.",
                    projectSpend, projectCap);
            }
        }

        return new SpendCapCheckResult(true, "Within spend caps.", globalSpend, opts.GlobalCapUsd);
    }
}
