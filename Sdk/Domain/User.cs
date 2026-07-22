namespace Telechron.Sdk.Domain;

// The human identity behind every approval and notification (R-DM15).
// AuthCredentialHash is an opaque hash (never a raw password/secret) —
// the actual auth scheme (R-SEC6) lands with Phase 2's API auth seam.
public sealed class User
{
    public required Guid Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public required string AuthCredentialHash { get; init; }
    public required Role Role { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
