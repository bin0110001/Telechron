using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class IntentPlanMapper
{
    public static IntentPlan ToDomain(this IntentPlanEntity entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        NaturalLanguageRequest = entity.NaturalLanguageRequest,
        PlanningPath = (IntentPlanningPath)entity.PlanningPath,
        ProposedWorkflowIdsJson = entity.ProposedWorkflowIdsJson,
        CapabilityGapAnalysisJson = entity.CapabilityGapAnalysisJson,
        RequiredModulesJson = entity.RequiredModulesJson,
        CreatedAtUtc = entity.CreatedAtUtc,
        AppliedAtUtc = entity.AppliedAtUtc,
    };

    public static IntentPlanEntity ToEntity(this IntentPlan domain) => new()
    {
        Id = domain.Id,
        ProjectId = domain.ProjectId,
        NaturalLanguageRequest = domain.NaturalLanguageRequest,
        PlanningPath = (int)domain.PlanningPath,
        ProposedWorkflowIdsJson = domain.ProposedWorkflowIdsJson,
        CapabilityGapAnalysisJson = domain.CapabilityGapAnalysisJson,
        RequiredModulesJson = domain.RequiredModulesJson,
        CreatedAtUtc = domain.CreatedAtUtc,
        AppliedAtUtc = domain.AppliedAtUtc,
    };

    public static void ApplyTo(this IntentPlan domain, IntentPlanEntity entity)
    {
        entity.CapabilityGapAnalysisJson = domain.CapabilityGapAnalysisJson;
        entity.RequiredModulesJson = domain.RequiredModulesJson;
        entity.AppliedAtUtc = domain.AppliedAtUtc;
    }
}
