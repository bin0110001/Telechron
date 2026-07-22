using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class LlmConnectionMapper
{
    public static LlmConnection ToDomain(this LlmConnectionEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Provider = entity.Provider,
        ConfigurationJson = entity.ConfigurationJson,
        SecretHandle = entity.SecretHandle,
        CreatedAtUtc = entity.CreatedAtUtc,
    };

    public static LlmConnectionEntity ToEntity(this LlmConnection domain) => new()
    {
        Id = domain.Id,
        Name = domain.Name,
        Provider = domain.Provider,
        ConfigurationJson = domain.ConfigurationJson,
        SecretHandle = domain.SecretHandle,
        CreatedAtUtc = domain.CreatedAtUtc,
    };

    public static void ApplyTo(this LlmConnection domain, LlmConnectionEntity entity)
    {
        entity.Name = domain.Name;
        entity.Provider = domain.Provider;
        entity.ConfigurationJson = domain.ConfigurationJson;
        entity.SecretHandle = domain.SecretHandle;
    }
}
