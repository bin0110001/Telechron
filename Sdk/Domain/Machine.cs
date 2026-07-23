namespace Telechron.Sdk.Domain;

// R-SCH3: MachineFingerprint is a stable hardware/OS identifier the Agent
// computes and presents at registration — re-registration from the same
// fingerprint updates this row instead of creating a duplicate Machine.
public sealed record Machine
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Hostname { get; init; }
    public required string MachineFingerprint { get; init; }
    public required DateTimeOffset RegisteredAtUtc { get; init; }
    // Null means the machine has registered but never sent a heartbeat yet.
    public DateTimeOffset? LastHeartbeatUtc { get; init; }
    public required bool IsOnline { get; init; }
}
