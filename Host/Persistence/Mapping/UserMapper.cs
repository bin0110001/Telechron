using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class UserMapper
{
    public static User ToDomain(this UserEntity entity) => new()
    {
        Id = entity.Id,
        DisplayName = entity.DisplayName,
        Email = entity.Email,
        AuthCredentialHash = entity.AuthCredentialHash,
        Role = (Role)entity.Role,
        CreatedAtUtc = entity.CreatedAtUtc,
    };

    public static UserEntity ToEntity(this User domain) => new()
    {
        Id = domain.Id,
        DisplayName = domain.DisplayName,
        Email = domain.Email,
        AuthCredentialHash = domain.AuthCredentialHash,
        Role = (int)domain.Role,
        CreatedAtUtc = domain.CreatedAtUtc,
    };

    public static void ApplyTo(this User domain, UserEntity entity)
    {
        entity.DisplayName = domain.DisplayName;
        entity.Email = domain.Email;
        entity.AuthCredentialHash = domain.AuthCredentialHash;
        entity.Role = (int)domain.Role;
        entity.CreatedAtUtc = domain.CreatedAtUtc;
    }
}
