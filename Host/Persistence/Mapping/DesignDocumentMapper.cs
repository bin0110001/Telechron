using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class DesignDocumentMapper
{
    public static DesignDocument ToDomain(this DesignDocumentEntity entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        Title = entity.Title,
        CreatedAtUtc = entity.CreatedAtUtc,
    };

    public static DesignDocumentEntity ToEntity(this DesignDocument domain) => new()
    {
        Id = domain.Id,
        ProjectId = domain.ProjectId,
        Title = domain.Title,
        CreatedAtUtc = domain.CreatedAtUtc,
    };

    public static void ApplyTo(this DesignDocument domain, DesignDocumentEntity entity)
    {
        entity.Title = domain.Title;
    }
}
