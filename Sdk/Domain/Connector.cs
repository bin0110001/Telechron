namespace Telechron.Sdk.Domain;

// A configured instance of a Connector Module bound to auth credentials
// (R-DM11, R-MOD9). ProjectId is nullable — "Connectors are reusable across
// projects," so null means globally shared and non-null scopes it to one
// Project. SecretHandle is opaque per R-SEC1 (never a raw credential).
// IsDeprecated is R-DM7a's retirement flag.
public sealed record Connector
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid ModuleId { get; init; }
    public required string Kind { get; init; }
    public required string ConfigurationJson { get; init; }
    public string? SecretHandle { get; init; }
    public required bool IsDeprecated { get; init; }
    public Guid? ProjectId { get; init; }
}
