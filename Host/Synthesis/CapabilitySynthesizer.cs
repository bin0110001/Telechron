namespace Telechron.Host.Synthesis;

using System.Text.Json;
using Telechron.Host.Llm;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules.Llm;
using Telechron.Sdk.Synthesis;

// R-BUILD3/R-DM6a: generates source + self-test for a missing capability
// via a real LLM call, carrying the Project's active Design Document as
// standing context -- same pattern as LlmFixGenerator (R-FIX2), not a
// separate mechanism (R-ENG4). DeclaredCapabilities is capped to exactly
// what the requesting Persona is allowed (R-MOD8a) -- the LLM is asked to
// declare only from that allowlist, and the result is filtered again
// server-side afterward so a model that ignores the instruction can't
// grant itself more.
public sealed class CapabilitySynthesizer(ILlmDispatcher dispatcher, LlmConnection connection) : ICapabilitySynthesizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SynthesizedCapabilityResult> SynthesizeModuleAsync(
        Guid projectId, string missingFunctionKind, DesignDocument? designDocument,
        IReadOnlyList<Requirement> activeRequirements, IReadOnlyList<string> personaAllowedCapabilities,
        CancellationToken ct = default)
    {
        var moduleName = $"telechron.functions.{missingFunctionKind}";
        var className = $"Synthesized{SanitizeForIdentifier(missingFunctionKind)}Module";

        var systemPrompt =
            "You are Telechron's capability synthesis engine. You generate a single C# source file implementing " +
            "Telechron.Sdk.Modules.Functions.IFunctionExecutorModule for the requested missing function kind, plus a " +
            "genuine self-test (a real behavioral check, not a stub that always passes -- a self-test that cannot fail " +
            "provides no evidence and will be rejected by the falsifiability gate). Respond with a JSON object: " +
            "{\"moduleSource\": \"...\", \"selfTestSource\": \"...\", \"declaredCapabilities\": [\"...\"]}. " +
            "declaredCapabilities MUST be a subset of the allowed list given in the instructions -- never invent one " +
            "outside it.";

        var instructions = BuildInstructions(missingFunctionKind, moduleName, className, designDocument, activeRequirements, personaAllowedCapabilities);

        var request = new LlmCompletionRequest(
            SystemPrompt: systemPrompt,
            Instructions: instructions,
            UntrustedContentBlocks: [],
            ModelOverride: string.Empty,
            Temperature: 0.2,
            MaxOutputTokens: 4096);

        var result = await dispatcher.DispatchAsync(connection, projectId, request, ct);

        if (!result.Succeeded)
        {
            return new SynthesizedCapabilityResult
            {
                Success = false, ModuleName = moduleName, FunctionKind = missingFunctionKind,
                SourceCode = string.Empty, SelfTestCode = string.Empty, Description = string.Empty,
                DeclaredCapabilities = [],
                ErrorMessage = result.ErrorMessage ?? "LLM call failed.",
            };
        }

        SynthesisEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<SynthesisEnvelope>(result.ResponseText, JsonOptions);
        }
        catch (JsonException ex)
        {
            return new SynthesizedCapabilityResult
            {
                Success = false, ModuleName = moduleName, FunctionKind = missingFunctionKind,
                SourceCode = string.Empty, SelfTestCode = string.Empty, Description = string.Empty,
                DeclaredCapabilities = [],
                ErrorMessage = $"Could not parse synthesis response: {ex.Message}",
            };
        }

        if (envelope is null || string.IsNullOrWhiteSpace(envelope.ModuleSource) || string.IsNullOrWhiteSpace(envelope.SelfTestSource))
        {
            return new SynthesizedCapabilityResult
            {
                Success = false, ModuleName = moduleName, FunctionKind = missingFunctionKind,
                SourceCode = string.Empty, SelfTestCode = string.Empty, Description = string.Empty,
                DeclaredCapabilities = [],
                ErrorMessage = "Synthesis response was missing module or self-test source.",
            };
        }

        // R-MOD8a: filter again server-side -- a system prompt instruction
        // is not an enforcement mechanism, only the allowlist intersection is.
        var allowedSet = personaAllowedCapabilities.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cappedCapabilities = (envelope.DeclaredCapabilities ?? [])
            .Where(c => allowedSet.Contains(c))
            .ToList();

        return new SynthesizedCapabilityResult
        {
            Success = true,
            ModuleName = moduleName,
            FunctionKind = missingFunctionKind,
            SourceCode = envelope.ModuleSource,
            SelfTestCode = envelope.SelfTestSource,
            DeclaredCapabilities = cappedCapabilities,
            Description = $"Synthesized function executor for '{missingFunctionKind}' with standing Design Document context.",
        };
    }

    private static string BuildInstructions(
        string missingFunctionKind, string moduleName, string className, DesignDocument? designDocument,
        IReadOnlyList<Requirement> activeRequirements, IReadOnlyList<string> personaAllowedCapabilities)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Missing function kind: {missingFunctionKind}");
        sb.AppendLine($"Module name to declare: {moduleName}");
        sb.AppendLine($"Class name to use: {className}");
        sb.AppendLine();
        sb.AppendLine($"Persona-allowed capabilities (declaredCapabilities MUST be a subset of this list): [{string.Join(", ", personaAllowedCapabilities)}]");
        sb.AppendLine();
        sb.AppendLine($"Design Document (standing context, R-DM6a): {designDocument?.Title ?? "(none)"}");
        sb.AppendLine("Active Requirements:");
        if (activeRequirements.Count == 0)
        {
            sb.AppendLine("(none active)");
        }
        else
        {
            foreach (var requirement in activeRequirements)
                sb.AppendLine($"- {requirement.RequirementId}: {requirement.Title} -- {requirement.Body}");
        }

        sb.AppendLine();
        sb.AppendLine("Implement IFunctionExecutorModule (Telechron.Sdk.Modules.Functions) fully, including a real " +
            "RunSelfTestAsync body that performs an actual check of the module's own InvokeInProcessAsync behavior " +
            "and can genuinely fail if that behavior is wrong.");

        return sb.ToString();
    }

    private static string SanitizeForIdentifier(string value) =>
        new(value.Where(char.IsLetterOrDigit).ToArray());

    private sealed record SynthesisEnvelope(string ModuleSource, string SelfTestSource, IReadOnlyList<string>? DeclaredCapabilities);
}
