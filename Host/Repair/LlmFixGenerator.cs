using System.Text.Json;
using Telechron.Host.Llm;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules.Llm;
using Telechron.Sdk.Repair;

namespace Telechron.Host.Repair;

// Default R-FIX2 LLM-path Generate Fix. R-DM6a: ActiveRequirements and
// DesignDocument are rendered into Instructions (trusted -- they came
// from the Project's own approved Design Document, R-DM16b) alongside the
// source file contents; only Finding.RootCauseSignature/Category text
// goes into UntrustedContentBlocks (R-LLM5), since that's the one part of
// the context that could originate from attacker-influenced input (a
// connector-fed CVE description, a poisoned test name).
public sealed class LlmFixGenerator(ILlmDispatcher dispatcher, LlmConnection connection) : ILlmFixGenerator
{
    private const string SynthesisMarker = "REQUIRES_CAPABILITY_SYNTHESIS";

    public async Task<LlmFixResult> GenerateAsync(LlmFixContext context, CancellationToken ct = default)
    {
        var systemPrompt =
            "You are Telechron's repair engine. You generate a unified diff patch that fixes the given " +
            "Finding(s) in the given source files, consistent with the Project's Design Document Requirements " +
            "provided as standing context. If the fix genuinely requires a new module/Function/capability " +
            $"rather than a patch to existing code, respond with exactly the line '{SynthesisMarker}' and nothing else.";

        var instructions = BuildInstructions(context);
        var untrustedBlocks = context.Findings
            .Select(f => new UntrustedContentBlock($"Finding {f.Id} ({f.Category})", f.RootCauseSignature))
            .ToList();

        var request = new LlmCompletionRequest(
            SystemPrompt: systemPrompt,
            Instructions: instructions,
            UntrustedContentBlocks: untrustedBlocks,
            ModelOverride: string.Empty,
            Temperature: 0.2,
            MaxOutputTokens: 4096);

        var result = await dispatcher.DispatchAsync(connection, context.Findings.FirstOrDefault()?.ProjectId, request, ct);

        if (!result.Succeeded)
            return new LlmFixResult(false, null, false, result.ErrorMessage ?? "LLM call failed.");

        if (result.ResponseText.Contains(SynthesisMarker, StringComparison.Ordinal))
            return new LlmFixResult(true, null, true, result.ResponseText);

        var patch = TryParsePatch(result.ResponseText);
        return patch is null
            ? new LlmFixResult(false, null, false, result.ResponseText)
            : new LlmFixResult(true, patch, false, result.ResponseText);
    }

    private static string BuildInstructions(LlmFixContext context)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Design Document Requirements (standing context, R-DM6a):");
        if (context.ActiveRequirements.Count == 0)
        {
            sb.AppendLine("(none active)");
        }
        else
        {
            foreach (var requirement in context.ActiveRequirements)
                sb.AppendLine($"- {requirement.RequirementId}: {requirement.Title} -- {requirement.Body}");
        }

        sb.AppendLine();
        sb.AppendLine("Relevant source files:");
        foreach (var (path, content) in context.RelevantFileContents)
        {
            sb.AppendLine($"--- {path} ---");
            sb.AppendLine(content);
        }

        sb.AppendLine();
        sb.AppendLine(
            "Respond with a unified diff patch (one or more file hunks, standard @@ -old,+new @@ headers) " +
            "that fixes the Finding(s) above without contradicting any Requirement listed.");

        return sb.ToString();
    }

    // Response format: a JSON envelope { "files": [ { "path": "...", "diff": "..." } ] } --
    // chosen over asking the model for a raw unified diff blob because it
    // keeps multi-file patches (R-FIX7) unambiguous to parse back into
    // PatchDiff without a hand-rolled multi-file-diff splitter.
    private static PatchDiff? TryParsePatch(string responseText)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<LlmPatchEnvelope>(responseText);
            if (envelope is null || envelope.Files.Count == 0)
                return null;

            var changes = envelope.Files
                .Select(f => new PatchFileChange(f.Path, f.Diff))
                .ToList();

            return new PatchDiff(changes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record LlmPatchEnvelope(IReadOnlyList<LlmPatchFile> Files);
    private sealed record LlmPatchFile(string Path, string Diff);
}
