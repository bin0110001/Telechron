using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class ConnectorMapper
{
    public static Connector ToDomain(this ConnectorEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        ModuleId = entity.ModuleId,
        Kind = entity.Kind,
        ConfigurationJson = entity.ConfigurationJson,
        SecretHandle = entity.SecretHandle,
        IsDeprecated = entity.IsDeprecated,
        ProjectId = entity.ProjectId,
    };

    public static ConnectorEntity ToEntity(this Connector domain) => new()
    {
        Id = domain.Id,
        Name = domain.Name,
        ModuleId = domain.ModuleId,
        Kind = domain.Kind,
        ConfigurationJson = domain.ConfigurationJson,
        SecretHandle = domain.SecretHandle,
        IsDeprecated = domain.IsDeprecated,
        ProjectId = domain.ProjectId,
    };

    public static void ApplyTo(this Connector domain, ConnectorEntity entity)
    {
        entity.Name = domain.Name;
        entity.ModuleId = domain.ModuleId;
        entity.Kind = domain.Kind;
        entity.ConfigurationJson = domain.ConfigurationJson;
        entity.SecretHandle = domain.SecretHandle;
        entity.IsDeprecated = domain.IsDeprecated;
        entity.ProjectId = domain.ProjectId;
    }
}
