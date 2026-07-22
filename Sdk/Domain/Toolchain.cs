namespace Telechron.Sdk.Domain;

// Defines how a project is built/tested/verified/exported/deployed (R-DM14). Toolchains
// are provided through modules, but Module isn't a persisted entity yet, so ModuleId is
// a plain reference with no FK enforcement until that entity lands.
public sealed record Toolchain
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid ModuleId { get; init; }
    public required string BuildCommand { get; init; }
    public required string TestCommand { get; init; }
    public required string VerifyCommand { get; init; }
    public string? ExportCommand { get; init; }
    public string? DeployCommand { get; init; }
    public required string EnvironmentRequirementsJson { get; init; }
}
