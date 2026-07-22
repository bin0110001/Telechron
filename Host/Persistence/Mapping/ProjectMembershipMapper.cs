using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class ProjectMembershipMapper
{
    public static ProjectMembership ToDomain(this ProjectMembershipEntity entity) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        ProjectId = entity.ProjectId,
        Role = (Role)entity.Role,
    };

    public static ProjectMembershipEntity ToEntity(this ProjectMembership domain) => new()
    {
        Id = domain.Id,
        UserId = domain.UserId,
        ProjectId = domain.ProjectId,
        Role = (int)domain.Role,
    };
}
