using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class ArtifactMapper
{
    public static Artifact ToDomain(this ArtifactEntity entity) => new()
    {
        Id = entity.Id,
        WorkflowRunId = entity.WorkflowRunId,
        Name = entity.Name,
        ArtifactType = entity.ArtifactType,
        BlobRef = entity.BlobRef,
        SizeBytes = entity.SizeBytes,
        CreatedAtUtc = entity.CreatedAtUtc,
    };

    public static ArtifactEntity ToEntity(this Artifact domain) => new()
    {
        Id = domain.Id,
        WorkflowRunId = domain.WorkflowRunId,
        Name = domain.Name,
        ArtifactType = domain.ArtifactType,
        BlobRef = domain.BlobRef,
        SizeBytes = domain.SizeBytes,
        CreatedAtUtc = domain.CreatedAtUtc,
    };

    public static void ApplyTo(this Artifact domain, ArtifactEntity entity)
    {
        entity.Name = domain.Name;
        entity.WorkflowRunId = domain.WorkflowRunId;
    }
}
