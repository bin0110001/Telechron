namespace Telechron.Sdk.Intent;

using Telechron.Sdk.Domain;

public interface IIntentPlanner
{
    Task<IntentPlan> CreatePlanAsync(Guid projectId, string naturalLanguageRequest, CancellationToken ct = default);
    Task<Workflow> ApplyPlanAsync(Guid intentPlanId, CancellationToken ct = default);
}
