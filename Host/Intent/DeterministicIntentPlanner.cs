namespace Telechron.Host.Intent;

using System.Text.Json;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Intent;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Workflows;

public sealed class DeterministicIntentPlanner(
    IWorkflowRepository workflowRepo,
    ICapabilityGapAnalyzer gapAnalyzer)
{
    public async Task<IntentPlan?> TryCreatePlanAsync(
        Guid projectId, string naturalLanguageRequest, CancellationToken ct = default)
    {
        _ = workflowRepo;
        var lower = naturalLanguageRequest.ToLowerInvariant().Trim();

        // Exact or rule match for standard workflow patterns (e.g. "zip repo", "run git clone")
        if (lower.Contains("zip") || lower.Contains("archive"))
        {
            var def = new WorkflowDefinition
            {
                Name = "Deterministic Zip Workflow",
                FailurePolicy = WorkflowFailurePolicy.FailFast,
                Steps =
                [
                    new WorkflowStepDefinition
                    {
                        Id = "step-zip",
                        Name = "Zip Source Directory",
                        FunctionKind = "zip",
                        Parameters = new Dictionary<string, string>
                        {
                            ["sourceDirectory"] = "src",
                            ["destinationZipPath"] = "output.zip"
                        },
                        OutputArtifactTypes = ["application/zip"]
                    }
                ]
            };

            var gapReport = await gapAnalyzer.AnalyzeGapsAsync(JsonSerializer.Serialize(def), ct);

            return new IntentPlan
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NaturalLanguageRequest = naturalLanguageRequest,
                PlanningPath = IntentPlanningPath.Deterministic,
                ProposedWorkflowIdsJson = JsonSerializer.Serialize(def),
                CapabilityGapAnalysisJson = gapReport.DetailsJson,
                RequiredModulesJson = JsonSerializer.Serialize(gapReport.MissingModuleTypes),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                AppliedAtUtc = null
            };
        }

        return null;
    }
}
