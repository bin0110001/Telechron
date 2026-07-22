using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class ToolchainMapper
{
    public static Toolchain ToDomain(this ToolchainEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        ModuleId = entity.ModuleId,
        BuildCommand = entity.BuildCommand,
        TestCommand = entity.TestCommand,
        VerifyCommand = entity.VerifyCommand,
        ExportCommand = entity.ExportCommand,
        DeployCommand = entity.DeployCommand,
        EnvironmentRequirementsJson = entity.EnvironmentRequirementsJson,
    };

    public static ToolchainEntity ToEntity(this Toolchain domain) => new()
    {
        Id = domain.Id,
        Name = domain.Name,
        ModuleId = domain.ModuleId,
        BuildCommand = domain.BuildCommand,
        TestCommand = domain.TestCommand,
        VerifyCommand = domain.VerifyCommand,
        ExportCommand = domain.ExportCommand,
        DeployCommand = domain.DeployCommand,
        EnvironmentRequirementsJson = domain.EnvironmentRequirementsJson,
    };

    public static void ApplyTo(this Toolchain domain, ToolchainEntity entity)
    {
        entity.Name = domain.Name;
        entity.ModuleId = domain.ModuleId;
        entity.BuildCommand = domain.BuildCommand;
        entity.TestCommand = domain.TestCommand;
        entity.VerifyCommand = domain.VerifyCommand;
        entity.ExportCommand = domain.ExportCommand;
        entity.DeployCommand = domain.DeployCommand;
        entity.EnvironmentRequirementsJson = domain.EnvironmentRequirementsJson;
    }
}
