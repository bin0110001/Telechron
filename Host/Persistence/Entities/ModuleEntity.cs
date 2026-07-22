namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for Module (R-DM7, R-DM7a).
public sealed class ModuleEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int VersionMajor { get; set; }
    public int VersionMinor { get; set; }
    public int VersionPatch { get; set; }
    public string CapabilitiesJson { get; set; } = string.Empty;
    public string TestCommand { get; set; } = string.Empty;
    public string SourceCodeRef { get; set; } = string.Empty;
    public DateTimeOffset InstalledAtUtc { get; set; }
}
