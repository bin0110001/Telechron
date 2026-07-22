namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for LlmConnection (R-DM10).
public sealed class LlmConnectionEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = string.Empty;
    public string? SecretHandle { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
