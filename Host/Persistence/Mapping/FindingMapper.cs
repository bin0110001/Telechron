using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class FindingMapper
{
    public static Finding ToDomain(this FindingEntity entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        RunId = entity.RunId,
        WorkflowRunId = entity.WorkflowRunId,
        OriginFilePath = entity.OriginFilePath,
        RootCauseSignature = entity.RootCauseSignature,
        Severity = (FindingSeverity)entity.Severity,
        Category = entity.Category,
        FailureClass = (FindingFailureClass)entity.FailureClass,
        Fixability = entity.Fixability,
        FixStatus = entity.FixStatus,
        CreatedAtUtc = entity.CreatedAtUtc,
    };

    public static FindingEntity ToEntity(this Finding domain) => new()
    {
        Id = domain.Id,
        ProjectId = domain.ProjectId,
        RunId = domain.RunId,
        WorkflowRunId = domain.WorkflowRunId,
        OriginFilePath = domain.OriginFilePath,
        RootCauseSignature = domain.RootCauseSignature,
        Severity = (int)domain.Severity,
        Category = domain.Category,
        FailureClass = (int)domain.FailureClass,
        Fixability = domain.Fixability,
        FixStatus = domain.FixStatus,
        CreatedAtUtc = domain.CreatedAtUtc,
    };

    public static void ApplyTo(this Finding domain, FindingEntity entity)
    {
        entity.RunId = domain.RunId;
        entity.WorkflowRunId = domain.WorkflowRunId;
        entity.OriginFilePath = domain.OriginFilePath;
        entity.RootCauseSignature = domain.RootCauseSignature;
        entity.Severity = (int)domain.Severity;
        entity.Category = domain.Category;
        entity.FailureClass = (int)domain.FailureClass;
        entity.Fixability = domain.Fixability;
        entity.FixStatus = domain.FixStatus;
    }
}
