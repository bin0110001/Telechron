namespace Telechron.Sdk.Domain;

// R-SEC2: an authenticated Agent connection session, issued at RegisterAgent
// and presented (as SessionTokenHash) on Heartbeat/SubscribeToDispatch calls.
// Persisted so a Host restart doesn't force every connected Agent to
// re-register (R-PER2). TokenHash, never the raw token — same handle-not-
// value discipline as R-SEC1's secret handles, since a session token is
// itself a bearer credential.
public sealed record AgentSession
{
    public required Guid Id { get; init; }
    public required Guid MachineId { get; init; }
    public required string SessionTokenHash { get; init; }
    public required DateTimeOffset IssuedAtUtc { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public DateTimeOffset? RevokedAtUtc { get; init; }
}
