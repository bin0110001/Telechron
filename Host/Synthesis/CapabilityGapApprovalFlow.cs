namespace Telechron.Host.Synthesis;

using System.Text.Json;
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
    ICapabilitySynthesizer synthesizer,
    ICapabilityVerificationRunner verificationRunner,
    IDesignDocumentRepository designDocRepo)
{
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
        var approvalPrompt = $"Missing capability '{missingKind}' requested by NL: '{naturalLanguageRequest}'. Approve synthesis and installation?";

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
        Guid projectId, IntentPlan plan, Guid approvalRequestId, CancellationToken ct = default)
    {
        var approvalRequest = await approvalManager.GetRequestByIdAsync(approvalRequestId, ct)
            ?? throw new KeyNotFoundException($"Approval request '{approvalRequestId}' was not found.");

        if (!approvalRequest.IsSatisfied)
        {
            throw new InvalidOperationException($"Human approval gate '{approvalRequestId}' has not been approved.");
        }

        var activeDesignDoc = await designDocRepo.GetByProjectAsync(projectId, ct);

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
            catch { }
        }

        // Synthesize module with Design Document standing context (R-BUILD3 / R-DM6a)
        var synthesized = await synthesizer.SynthesizeModuleAsync(projectId, missingKind, activeDesignDoc, ct);

        // Verify module in container with architectural drift detection (R-FIX13)
        var verification = await verificationRunner.VerifySynthesizedModuleAsync(projectId, synthesized, activeDesignDoc, ct);

        return new CapabilityGapFlowResult
        {
            Plan = plan,
            RequiresHumanApproval = false,
            ApprovalRequest = approvalRequest,
            SynthesizedModule = synthesized,
            VerificationResult = verification
        };
    }
}
