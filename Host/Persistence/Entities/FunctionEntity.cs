namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for Function (R-DM4, R-DM7a).
public sealed class FunctionEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ModuleId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string InputArtifactTypesJson { get; set; } = string.Empty;
    public string OutputArtifactTypesJson { get; set; } = string.Empty;
    public bool IsDeprecated { get; set; }
    public int ModuleVersionMajor { get; set; }
    public int ModuleVersionMinor { get; set; }
}
