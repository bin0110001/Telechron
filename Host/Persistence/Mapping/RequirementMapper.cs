using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class RequirementMapper
{
    public static Requirement ToDomain(this RequirementEntity entity) => new()
    {
        Id = entity.Id,
        DesignDocumentId = entity.DesignDocumentId,
        RequirementId = entity.RequirementId,
        Title = entity.Title,
        Body = entity.Body,
        Status = (RequirementStatus)entity.Status,
        CurrentRevisionNumber = entity.CurrentRevisionNumber,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
    };

    public static RequirementEntity ToEntity(this Requirement domain) => new()
    {
        Id = domain.Id,
        DesignDocumentId = domain.DesignDocumentId,
        RequirementId = domain.RequirementId,
        Title = domain.Title,
        Body = domain.Body,
        Status = (int)domain.Status,
        CurrentRevisionNumber = domain.CurrentRevisionNumber,
        CreatedAtUtc = domain.CreatedAtUtc,
        UpdatedAtUtc = domain.UpdatedAtUtc,
    };

    public static void ApplyTo(this Requirement domain, RequirementEntity entity)
    {
        entity.Title = domain.Title;
        entity.Body = domain.Body;
        entity.Status = (int)domain.Status;
        entity.CurrentRevisionNumber = domain.CurrentRevisionNumber;
        entity.UpdatedAtUtc = domain.UpdatedAtUtc;
    }
}
