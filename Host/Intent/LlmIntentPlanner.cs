namespace Telechron.Host.Intent;

using System.Text.Json;
using Telechron.Host.Llm;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Intent;
using Telechron.Sdk.Modules.Llm;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Workflows;

// R-BUILD1: the actual LLM fallback path -- reached only when
// DeterministicIntentPlanner's rule/pattern matching declines. A genuine
// LLM call translates the NL request into a WorkflowDefinition; this is
// NOT a second layer of hardcoded keyword matching (the bug this
// replaces), since that would make "falls back to LLM" a false claim.
public sealed class LlmIntentPlanner(
    ILlmDispatcher llmDispatcher,
    ILlmConnectionRepository llmConnectionRepository,
    IProjectRepository projectRepository,
    ICapabilityGapAnalyzer gapAnalyzer)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IntentPlan> CreatePlanAsync(
        Guid projectId, string naturalLanguageRequest, CancellationToken ct = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, ct)
            ?? throw new InvalidOperationException($"Project '{projectId}' does not exist.");
        if (project.LlmConnectionId is not { } llmConnectionId)
            throw new InvalidOperationException($"Project '{projectId}' has no LlmConnection assigned -- cannot fall back to LLM-driven intent planning.");
        var connection = await llmConnectionRepository.GetByIdAsync(llmConnectionId, ct)
            ?? throw new InvalidOperationException($"LlmConnection '{llmConnectionId}' referenced by Project '{projectId}' no longer exists.");

        var systemPrompt =
            "You are Telechron's intent planning engine. Translate the user's natural-language request into a " +
            "WorkflowDefinition: a sequence of steps, each naming a FunctionKind (a short, lowercase-hyphenated " +
            "capability name such as 'git', 'zip', 'deploy', or a specific custom name if the request needs " +
            "something not in that list) and any Parameters it needs. Respond with a JSON object: " +
            "{\"workflowName\": \"...\", \"steps\": [{\"id\": \"step-1\", \"name\": \"...\", \"functionKind\": \"...\", " +
            "\"parameters\": {\"key\": \"value\"}}]}.";

        var request = new LlmCompletionRequest(
            SystemPrompt: systemPrompt,
            Instructions: $"Natural language request: {naturalLanguageRequest}",
            UntrustedContentBlocks: [],
            ModelOverride: string.Empty,
            Temperature: 0.2,
            MaxOutputTokens: 2048);

        var result = await llmDispatcher.DispatchAsync(connection, projectId, request, ct);

        var def = result.Succeeded ? TryParseWorkflowDefinition(result.ResponseText, naturalLanguageRequest) : null;
        def ??= FallbackSingleStepDefinition(naturalLanguageRequest);

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

    private static WorkflowDefinition? TryParseWorkflowDefinition(string responseText, string naturalLanguageRequest)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<LlmWorkflowEnvelope>(responseText, JsonOptions);
            if (envelope is null || envelope.Steps.Count == 0)
                return null;

            return new WorkflowDefinition
            {
                Name = string.IsNullOrWhiteSpace(envelope.WorkflowName) ? $"NL Generated Workflow ({naturalLanguageRequest})" : envelope.WorkflowName,
                FailurePolicy = WorkflowFailurePolicy.FailFast,
                Steps = envelope.Steps
                    .Select(s => new WorkflowStepDefinition
                    {
                        Id = s.Id,
                        Name = s.Name,
                        FunctionKind = s.FunctionKind,
                        Parameters = s.Parameters ?? new Dictionary<string, string>(),
                    })
                    .ToList(),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Reached only if the LLM call itself fails or returns an unparseable
    // response -- an availability fallback, not a substitute for the LLM
    // path under normal operation.
    private static WorkflowDefinition FallbackSingleStepDefinition(string naturalLanguageRequest) => new()
    {
        Name = $"NL Generated Workflow ({naturalLanguageRequest})",
        FailurePolicy = WorkflowFailurePolicy.FailFast,
        Steps =
        [
            new WorkflowStepDefinition
            {
                Id = "step-1",
                Name = "Execute custom-function",
                FunctionKind = "custom-function",
                Parameters = new Dictionary<string, string> { ["input"] = naturalLanguageRequest },
            }
        ],
    };

    private sealed record LlmWorkflowStep(string Id, string Name, string FunctionKind, Dictionary<string, string>? Parameters);
    private sealed record LlmWorkflowEnvelope(string WorkflowName, IReadOnlyList<LlmWorkflowStep> Steps);
}
