using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class WorkflowMapper
{
    public static Workflow ToDomain(this WorkflowEntity entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        Name = entity.Name,
        DefinitionJson = entity.DefinitionJson,
        FailurePolicy = (WorkflowFailurePolicy)entity.FailurePolicy,
        CreatedAtUtc = entity.CreatedAtUtc,
    };

    public static WorkflowEntity ToEntity(this Workflow domain) => new()
    {
        Id = domain.Id,
        ProjectId = domain.ProjectId,
        Name = domain.Name,
        DefinitionJson = domain.DefinitionJson,
        FailurePolicy = (int)domain.FailurePolicy,
        CreatedAtUtc = domain.CreatedAtUtc,
    };

    public static void ApplyTo(this Workflow domain, WorkflowEntity entity)
    {
        entity.Name = domain.Name;
        entity.DefinitionJson = domain.DefinitionJson;
        entity.FailurePolicy = (int)domain.FailurePolicy;
    }
}
