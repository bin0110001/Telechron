namespace Telechron.Host.Intent;

using System.Text.Json;
using Telechron.Host.Llm;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Intent;
using Telechron.Sdk.Workflows;

public sealed class LlmIntentPlanner(
    ILlmDispatcher? llmDispatcher,
    ICapabilityGapAnalyzer gapAnalyzer)
{
    public async Task<IntentPlan> CreatePlanAsync(
        Guid projectId, string naturalLanguageRequest, CancellationToken ct = default)
    {
        _ = llmDispatcher; // Optional LLM dispatcher seam
        var functionKind = ExtractRequestedFunctionKind(naturalLanguageRequest);

        var def = new WorkflowDefinition
        {
            Name = $"NL Generated Workflow ({naturalLanguageRequest})",
            FailurePolicy = WorkflowFailurePolicy.FailFast,
            Steps =
            [
                new WorkflowStepDefinition
                {
                    Id = "step-1",
                    Name = $"Execute {functionKind}",
                    FunctionKind = functionKind,
                    Parameters = new Dictionary<string, string>
                    {
                        ["input"] = naturalLanguageRequest
                    }
                }
            ]
        };

        var jsonDef = JsonSerializer.Serialize(def);
        var gapReport = await gapAnalyzer.AnalyzeGapsAsync(jsonDef, ct);

        return new IntentPlan
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            NaturalLanguageRequest = naturalLanguageRequest,
            PlanningPath = IntentPlanningPath.PersonaDriven,
            ProposedWorkflowIdsJson = jsonDef,
            CapabilityGapAnalysisJson = gapReport.DetailsJson,
            RequiredModulesJson = JsonSerializer.Serialize(gapReport.MissingModuleTypes),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            AppliedAtUtc = null
        };
    }

    private static string ExtractRequestedFunctionKind(string request)
    {
        var lower = request.ToLowerInvariant();
        if (lower.Contains("git") || lower.Contains("clone")) return "git";
        if (lower.Contains("zip")) return "zip";
        if (lower.Contains("deploy") || lower.Contains("publish")) return "deploy";
        return "custom-function";
    }
}
