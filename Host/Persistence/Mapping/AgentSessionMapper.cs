using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class AgentSessionMapper
{
    public static AgentSession ToDomain(this AgentSessionEntity entity) => new()
    {
        Id = entity.Id,
        MachineId = entity.MachineId,
        SessionTokenHash = entity.SessionTokenHash,
        IssuedAtUtc = entity.IssuedAtUtc,
        ExpiresAtUtc = entity.ExpiresAtUtc,
        RevokedAtUtc = entity.RevokedAtUtc,
    };

    public static AgentSessionEntity ToEntity(this AgentSession domain) => new()
    {
        Id = domain.Id,
        MachineId = domain.MachineId,
        SessionTokenHash = domain.SessionTokenHash,
        IssuedAtUtc = domain.IssuedAtUtc,
        ExpiresAtUtc = domain.ExpiresAtUtc,
        RevokedAtUtc = domain.RevokedAtUtc,
    };

    public static void ApplyTo(this AgentSession domain, AgentSessionEntity entity)
    {
        entity.RevokedAtUtc = domain.RevokedAtUtc;
    }
}
