using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Repair;

public sealed record LlmFixContext(
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<Requirement> ActiveRequirements,
    DesignDocument? DesignDocument,
    IReadOnlyDictionary<string, string> RelevantFileContents);

public sealed record LlmFixResult(bool Succeeded, PatchDiff? Patch, bool RequiresCapabilitySynthesis, string RawResponse);

// R-FIX2's Generate Fix, LLM path (reached only after R-FIX5's
// deterministic path declines). R-DM6a: the Project's active Design
// Document Requirements are ALWAYS part of LlmFixContext -- not an
// optional tool call the Persona might skip -- so a generated fix is
// checked against stated intent, not source code alone. Finding text is
// untrusted content (R-LLM5); the implementation is responsible for
// routing it through UntrustedContentBlocks, never string-concatenated
// into the system prompt.
public interface ILlmFixGenerator
{
    Task<LlmFixResult> GenerateAsync(LlmFixContext context, CancellationToken ct = default);
}
