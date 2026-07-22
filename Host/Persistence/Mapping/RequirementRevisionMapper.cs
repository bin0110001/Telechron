using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class RequirementRevisionMapper
{
    public static RequirementRevision ToDomain(this RequirementRevisionEntity entity) => new()
    {
        Id = entity.Id,
        RequirementId = entity.RequirementId,
        RevisionNumber = entity.RevisionNumber,
        Title = entity.Title,
        Body = entity.Body,
        Status = (RequirementStatus)entity.Status,
        ChangedByUserId = entity.ChangedByUserId,
        ChangeReason = entity.ChangeReason,
        CreatedAtUtc = entity.CreatedAtUtc,
    };

    public static RequirementRevisionEntity ToEntity(this RequirementRevision domain) => new()
    {
        Id = domain.Id,
        RequirementId = domain.RequirementId,
        RevisionNumber = domain.RevisionNumber,
        Title = domain.Title,
        Body = domain.Body,
        Status = (int)domain.Status,
        ChangedByUserId = domain.ChangedByUserId,
        ChangeReason = domain.ChangeReason,
        CreatedAtUtc = domain.CreatedAtUtc,
    };
}
