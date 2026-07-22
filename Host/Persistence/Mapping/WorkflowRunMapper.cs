using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class WorkflowRunMapper
{
    public static WorkflowRun ToDomain(this WorkflowRunEntity entity) => new()
    {
        Id = entity.Id,
        WorkflowId = entity.WorkflowId,
        Status = (WorkflowRunStatus)entity.Status,
        DefinitionSnapshotJson = entity.DefinitionSnapshotJson,
        StartedAtUtc = entity.StartedAtUtc,
        CompletedAtUtc = entity.CompletedAtUtc,
    };

    public static WorkflowRunEntity ToEntity(this WorkflowRun domain) => new()
    {
        Id = domain.Id,
        WorkflowId = domain.WorkflowId,
        Status = (int)domain.Status,
        DefinitionSnapshotJson = domain.DefinitionSnapshotJson,
        StartedAtUtc = domain.StartedAtUtc,
        CompletedAtUtc = domain.CompletedAtUtc,
    };

    public static void ApplyTo(this WorkflowRun domain, WorkflowRunEntity entity)
    {
        entity.Status = (int)domain.Status;
        entity.StartedAtUtc = domain.StartedAtUtc;
        entity.CompletedAtUtc = domain.CompletedAtUtc;
    }
}
