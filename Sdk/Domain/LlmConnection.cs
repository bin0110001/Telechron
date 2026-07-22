namespace Telechron.Sdk.Domain;

// Named LLM configuration (R-DM10). Provider is free-form string, not an enum,
// because providers are added via modules (R-LLM1/R-LLM2) in a later phase and
// the domain layer must not hardcode a closed set. SecretHandle is nullable —
// local providers like Ollama may need no API key.
public sealed record LlmConnection
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Provider { get; init; }
    public required string ConfigurationJson { get; init; }
    public string? SecretHandle { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
