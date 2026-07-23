using Telechron.Host.Llm;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules.Llm;
using Telechron.Sdk.Repair;

namespace Telechron.Host.Repair;

// Default R-FIX13 implementation. Asks the LLM to judge the finished
// patch against the Requirement bodies it's tagged/scoped against --
// deliberately a SEPARATE call from Generate Fix (not "trust whatever
// Generate Fix itself claimed"), since the whole point of R-FIX13 is an
// independent check that catches a patch which satisfies its Finding
// while quietly contradicting a Requirement the generator didn't weigh
// heavily enough.
public sealed class ArchitecturalDriftDetector(ILlmDispatcher dispatcher, LlmConnection connection) : IArchitecturalDriftDetector
{
    private const string NoDriftMarker = "NO_DRIFT";

    public async Task<DriftCheckResult> CheckAsync(PatchDiff patch, IReadOnlyList<Requirement> activeRequirements, CancellationToken ct = default)
    {
        if (activeRequirements.Count == 0)
            return new DriftCheckResult(false, "No Active Requirements to check the patch against.");

        var systemPrompt =
            "You are Telechron's architectural drift checker. Given a patch and a set of Active Requirements, " +
            $"determine whether the patch contradicts, narrows, or broadens any Requirement's stated contract. " +
            $"Respond with exactly '{NoDriftMarker}' if there is no contradiction, otherwise respond with a short " +
            "explanation of which Requirement is contradicted and how.";

        var instructions = BuildInstructions(patch, activeRequirements);

        var request = new LlmCompletionRequest(
            SystemPrompt: systemPrompt,
            Instructions: instructions,
            UntrustedContentBlocks: [],
            ModelOverride: string.Empty,
            Temperature: 0.0,
            MaxOutputTokens: 1024);

        var result = await dispatcher.DispatchAsync(connection, null, request, ct);

        if (!result.Succeeded)
        {
            // A failed drift check cannot be treated as "no drift" --
            // fail closed by reporting drift so the pipeline routes to
            // RequireApproval rather than silently committing unchecked.
            return new DriftCheckResult(true, $"Drift check could not be completed ({result.ErrorMessage}); routing to approval out of caution.");
        }

        var responseTrimmed = result.ResponseText.Trim();
        return responseTrimmed.StartsWith(NoDriftMarker, StringComparison.Ordinal)
            ? new DriftCheckResult(false, null)
            : new DriftCheckResult(true, responseTrimmed);
    }

    private static string BuildInstructions(PatchDiff patch, IReadOnlyList<Requirement> activeRequirements)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Active Requirements:");
        foreach (var requirement in activeRequirements)
            sb.AppendLine($"- {requirement.RequirementId}: {requirement.Title} -- {requirement.Body}");

        sb.AppendLine();
        sb.AppendLine("Patch under review:");
        foreach (var change in patch.FileChanges)
        {
            sb.AppendLine($"--- {change.RelativePath} ---");
            sb.AppendLine(change.UnifiedDiff);
        }

        return sb.ToString();
    }
}
