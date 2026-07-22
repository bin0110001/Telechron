namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for Toolchain (R-DM14).
public sealed class ToolchainEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ModuleId { get; set; }
    public string BuildCommand { get; set; } = string.Empty;
    public string TestCommand { get; set; } = string.Empty;
    public string VerifyCommand { get; set; } = string.Empty;
    public string? ExportCommand { get; set; }
    public string? DeployCommand { get; set; }
    public string EnvironmentRequirementsJson { get; set; } = string.Empty;
}
