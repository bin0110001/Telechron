namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for Connector (R-DM11, R-MOD9, R-DM7a).
public sealed class ConnectorEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ModuleId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = string.Empty;
    public string? SecretHandle { get; set; }
    public bool IsDeprecated { get; set; }
    public Guid? ProjectId { get; set; }

    public ProjectEntity? Project { get; set; }
}
