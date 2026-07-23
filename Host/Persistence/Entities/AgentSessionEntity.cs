namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for AgentSession (R-SEC2).
public sealed class AgentSessionEntity
{
    public Guid Id { get; set; }
    public Guid MachineId { get; set; }
    public string SessionTokenHash { get; set; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }

    public MachineEntity? Machine { get; set; }
}
