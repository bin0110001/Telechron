// EF Core-mapped row shape for Machine (R-DM8).
namespace Telechron.Host.Persistence.Entities;

public sealed class MachineEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public DateTimeOffset RegisteredAtUtc { get; set; }
    public DateTimeOffset? LastHeartbeatUtc { get; set; }
    public bool IsOnline { get; set; }
}
