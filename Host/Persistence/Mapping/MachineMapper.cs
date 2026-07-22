using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class MachineMapper
{
    public static Machine ToDomain(this MachineEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Hostname = entity.Hostname,
        RegisteredAtUtc = entity.RegisteredAtUtc,
        LastHeartbeatUtc = entity.LastHeartbeatUtc,
        IsOnline = entity.IsOnline,
    };

    public static MachineEntity ToEntity(this Machine domain) => new()
    {
        Id = domain.Id,
        Name = domain.Name,
        Hostname = domain.Hostname,
        RegisteredAtUtc = domain.RegisteredAtUtc,
        LastHeartbeatUtc = domain.LastHeartbeatUtc,
        IsOnline = domain.IsOnline,
    };

    public static void ApplyTo(this Machine domain, MachineEntity entity)
    {
        entity.Name = domain.Name;
        entity.Hostname = domain.Hostname;
        entity.LastHeartbeatUtc = domain.LastHeartbeatUtc;
        entity.IsOnline = domain.IsOnline;
    }
}
