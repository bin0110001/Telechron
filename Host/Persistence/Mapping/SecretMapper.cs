using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class SecretMapper
{
    public static Secret ToDomain(this SecretEntity entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        Handle = entity.Handle,
        Name = entity.Name,
        EncryptedValue = entity.EncryptedValue,
        EncryptionKeyId = entity.EncryptionKeyId,
        CreatedAtUtc = entity.CreatedAtUtc,
        RevokedAtUtc = entity.RevokedAtUtc,
    };

    public static SecretEntity ToEntity(this Secret domain) => new()
    {
        Id = domain.Id,
        ProjectId = domain.ProjectId,
        Handle = domain.Handle,
        Name = domain.Name,
        EncryptedValue = domain.EncryptedValue,
        EncryptionKeyId = domain.EncryptionKeyId,
        CreatedAtUtc = domain.CreatedAtUtc,
        RevokedAtUtc = domain.RevokedAtUtc,
    };

    public static void ApplyTo(this Secret domain, SecretEntity entity)
    {
        entity.Handle = domain.Handle;
        entity.Name = domain.Name;
        entity.EncryptedValue = domain.EncryptedValue;
        entity.EncryptionKeyId = domain.EncryptionKeyId;
        entity.RevokedAtUtc = domain.RevokedAtUtc;
    }
}
