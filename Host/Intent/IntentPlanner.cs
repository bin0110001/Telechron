namespace Telechron.Host.Intent;

using System.Text.Json;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Intent;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Workflows;

public sealed class IntentPlanner(
    DeterministicIntentPlanner deterministicPlanner,
    LlmIntentPlanner llmPlanner,
    IIntentPlanRepository planRepo,
    IWorkflowRepository workflowRepo) : IIntentPlanner
{
    public async Task<IntentPlan> CreatePlanAsync(
        Guid projectId, string naturalLanguageRequest, CancellationToken ct = default)
    {
        var plan = await deterministicPlanner.TryCreatePlanAsync(projectId, naturalLanguageRequest, ct)
            ?? await llmPlanner.CreatePlanAsync(projectId, naturalLanguageRequest, ct);

        await planRepo.AddAsync(plan, ct);
        return plan;
    }

    public async Task<Workflow> ApplyPlanAsync(Guid intentPlanId, CancellationToken ct = default)
    {
        var plan = await planRepo.GetByIdAsync(intentPlanId, ct)
            ?? throw new KeyNotFoundException($"IntentPlan '{intentPlanId}' was not found.");

        if (plan.AppliedAtUtc is not null)
        {
            throw new InvalidOperationException($"IntentPlan '{intentPlanId}' has already been applied.");
        }

        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            ProjectId = plan.ProjectId,
            Name = $"Workflow from Plan ({plan.Id.ToString()[..8]})",
            DefinitionJson = plan.ProposedWorkflowIdsJson,
            FailurePolicy = WorkflowFailurePolicy.FailFast,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await workflowRepo.AddAsync(workflow, ct);

        var updatedPlan = plan with { AppliedAtUtc = DateTimeOffset.UtcNow };
        await planRepo.UpdateAsync(updatedPlan, ct);

        return workflow;
    }
}
