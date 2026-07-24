using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class ProjectMapper
{
    public static Project ToDomain(this ProjectEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        RootPath = entity.RootPath,
        OwnerUserId = entity.OwnerUserId,
        RepairPolicy = (RepairPolicy)entity.RepairPolicy,
        ToolchainId = entity.ToolchainId,
        LlmConnectionId = entity.LlmConnectionId,
        CreatedAtUtc = entity.CreatedAtUtc,
    };

    public static ProjectEntity ToEntity(this Project domain) => new()
    {
        Id = domain.Id,
        Name = domain.Name,
        RootPath = domain.RootPath,
        OwnerUserId = domain.OwnerUserId,
        RepairPolicy = (int)domain.RepairPolicy,
        ToolchainId = domain.ToolchainId,
        LlmConnectionId = domain.LlmConnectionId,
        CreatedAtUtc = domain.CreatedAtUtc,
    };

    public static void ApplyTo(this Project domain, ProjectEntity entity)
    {
        entity.Name = domain.Name;
        entity.RootPath = domain.RootPath;
        entity.RepairPolicy = (int)domain.RepairPolicy;
        entity.ToolchainId = domain.ToolchainId;
        entity.LlmConnectionId = domain.LlmConnectionId;
    }
}
