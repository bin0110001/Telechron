using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class ResourceMapper
{
    public static Resource ToDomain(this ResourceEntity entity) => new()
    {
        Id = entity.Id,
        MachineId = entity.MachineId,
        Kind = entity.Kind,
        Name = entity.Name,
        ExclusiveGroup = entity.ExclusiveGroup,
    };

    public static ResourceEntity ToEntity(this Resource domain) => new()
    {
        Id = domain.Id,
        MachineId = domain.MachineId,
        Kind = domain.Kind,
        Name = domain.Name,
        ExclusiveGroup = domain.ExclusiveGroup,
    };

    public static void ApplyTo(this Resource domain, ResourceEntity entity)
    {
        entity.Kind = domain.Kind;
        entity.Name = domain.Name;
        entity.ExclusiveGroup = domain.ExclusiveGroup;
    }
}
