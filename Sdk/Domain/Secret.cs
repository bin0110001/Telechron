namespace Telechron.Sdk.Domain;

// Encrypted project-owned configuration (R-DM12). Referenced only by Handle —
// Personas/prompts never see raw values (R-SEC1, wired in Phase 2). EncryptedValue
// and EncryptionKeyId are opaque to the domain layer; Phase 2 adds the external
// key-management resolution behind IsRevoked/rotation (R-SEC8, R-SEC9).
public sealed record Secret
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required string Handle { get; init; }
    public required string Name { get; init; }
    public required byte[] EncryptedValue { get; init; }
    public required string EncryptionKeyId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? RevokedAtUtc { get; init; }
}
