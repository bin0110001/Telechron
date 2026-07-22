namespace Telechron.Sdk.Domain;

public sealed record Machine
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Hostname { get; init; }
    public required DateTimeOffset RegisteredAtUtc { get; init; }
    // Null means the machine has registered but never sent a heartbeat yet.
    public DateTimeOffset? LastHeartbeatUtc { get; init; }
    public required bool IsOnline { get; init; }
}
