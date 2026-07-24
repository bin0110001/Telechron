namespace Telechron.Host.Synthesis;

using System.Text.Json;
using Telechron.Host.Llm;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Intent;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Synthesis;
using Telechron.Sdk.Workflows.Approvals;

public sealed record CapabilityGapFlowResult
{
    public required IntentPlan Plan { get; init; }
    public required bool RequiresHumanApproval { get; init; }
    public WorkflowApprovalRequest? ApprovalRequest { get; init; }
    public SynthesizedCapabilityResult? SynthesizedModule { get; init; }
    public CapabilityVerificationResult? VerificationResult { get; init; }
}

public sealed class CapabilityGapApprovalFlow(
    IIntentPlanner intentPlanner,
    ICapabilityGapAnalyzer gapAnalyzer,
    IApprovalManager approvalManager,
    ILlmDispatcher llmDispatcher,
    IProjectRepository projectRepository,
    ILlmConnectionRepository llmConnectionRepository,
    ICapabilityVerificationRunner verificationRunner,
    IDesignDocumentRepository designDocRepo,
    IRequirementRepository requirementRepo)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CapabilityGapFlowResult> ProcessRequestAsync(
        Guid projectId, string naturalLanguageRequest, CancellationToken ct = default)
    {
        // 1. Convert NL to side-effect-free Intent Plan
        var plan = await intentPlanner.CreatePlanAsync(projectId, naturalLanguageRequest, ct);

        // 2. Analyze capability gaps
        var gapReport = await gapAnalyzer.AnalyzeGapsAsync(plan.ProposedWorkflowIdsJson, ct);

        if (!gapReport.HasGaps)
        {
            return new CapabilityGapFlowResult
            {
                Plan = plan,
                RequiresHumanApproval = false
            };
        }

        // 3. Capability Gap identified -- MUST pass Human Approval Gate (R-BUILD5)
        var missingKind = gapReport.MissingFunctionKinds.Count > 0 ? gapReport.MissingFunctionKinds[0] : "custom-function";
        var approvalPrompt =
            $"Missing capability '{missingKind}' requested by NL: '{naturalLanguageRequest}'. Approve synthesis and " +
            "installation? Approval decision must specify which capabilities (R-MOD8a CapabilityKind values, e.g. " +
            "FilesystemRead) this synthesized module is granted, as a JSON array in the decision's parameter overrides.";

        var approvalRequest = await approvalManager.CreateRequestAsync(
            plan.Id, "synthesis-step", "R-BUILD5-human-gate", approvalPrompt, ct);

        return new CapabilityGapFlowResult
        {
            Plan = plan,
            RequiresHumanApproval = true,
            ApprovalRequest = approvalRequest
        };
    }

    public async Task<CapabilityGapFlowResult> ExecuteSynthesisAfterApprovalAsync(
        Guid projectId, IntentPlan plan, Guid approvalRequestId, Guid machineId, CancellationToken ct = default)
    {
        var approvalRequest = await approvalManager.GetRequestByIdAsync(approvalRequestId, ct)
            ?? throw new KeyNotFoundException($"Approval request '{approvalRequestId}' was not found.");

        if (!approvalRequest.IsSatisfied)
        {
            throw new InvalidOperationException($"Human approval gate '{approvalRequestId}' has not been approved.");
        }

        // R-MOD8a: the human approval decision is the actual capability
        // grant for a first install (IModuleTrustEvaluator's own doc
        // comment: "for a first install, it's whatever the human approval
        // flow granted") -- read from ParameterOverridesJson rather than
        // any Persona-derived allowlist, since nothing today links an
        // Intent Plan/NL request to a specific Persona.
        var approvedCapabilities = ParseApprovedCapabilities(approvalRequest.ParameterOverridesJson);

        var activeDesignDoc = await designDocRepo.GetByProjectAsync(projectId, ct);
        var activeRequirements = activeDesignDoc is not null
            ? (await requirementRepo.GetByDesignDocumentAsync(activeDesignDoc.Id, ct))
                .Where(r => r.Status == RequirementStatus.Active)
                .ToList()
            : [];

        var missingKind = "custom-function";
        if (plan.RequiredModulesJson is not null)
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(plan.RequiredModulesJson);
                if (list is not null && list.Count > 0)
                {
                    missingKind = list[0].Replace("function-executor:", "");
                }
            }
            catch (JsonException) { /* fall through to the default missingKind */ }
        }

        // Same reasoning as RepairPipelineFactory: CapabilitySynthesizer needs
        // this Project's real LlmConnection, so it's constructed per-call
        // rather than taken as a fixed DI dependency.
        var project = await projectRepository.GetByIdAsync(projectId, ct)
            ?? throw new InvalidOperationException($"Project '{projectId}' does not exist.");
        if (project.LlmConnectionId is not { } llmConnectionId)
            throw new InvalidOperationException($"Project '{projectId}' has no LlmConnection assigned -- cannot synthesize a capability.");
        var llmConnection = await llmConnectionRepository.GetByIdAsync(llmConnectionId, ct)
            ?? throw new InvalidOperationException($"LlmConnection '{llmConnectionId}' referenced by Project '{projectId}' no longer exists.");
        var synthesizer = new CapabilitySynthesizer(llmDispatcher, llmConnection);

        // Synthesize module with Design Document standing context (R-BUILD3 / R-DM6a),
        // capped to exactly the approved capabilities (R-MOD8a).
        var synthesized = await synthesizer.SynthesizeModuleAsync(
            projectId, missingKind, activeDesignDoc, activeRequirements, approvedCapabilities, ct);

        // Verify: real build + self-test in a container, real drift check
        // against real Requirements, real pre-trust pipeline (R-FIX13/R-MOD5a/R-MOD5b).
        var verification = await verificationRunner.VerifySynthesizedModuleAsync(
            projectId, synthesized, activeDesignDoc, machineId, activeRequirements, approvedCapabilities, ct);

        return new CapabilityGapFlowResult
        {
            Plan = plan,
            RequiresHumanApproval = false,
            ApprovalRequest = approvalRequest,
            SynthesizedModule = synthesized,
            VerificationResult = verification
        };
    }

    private static IReadOnlyList<string> ParseApprovedCapabilities(string? parameterOverridesJson)
    {
        if (string.IsNullOrWhiteSpace(parameterOverridesJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(parameterOverridesJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
