using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class ModuleMapper
{
    public static Module ToDomain(this ModuleEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Kind = entity.Kind,
        VersionMajor = entity.VersionMajor,
        VersionMinor = entity.VersionMinor,
        VersionPatch = entity.VersionPatch,
        CapabilitiesJson = entity.CapabilitiesJson,
        TestCommand = entity.TestCommand,
        SourceCodeRef = entity.SourceCodeRef,
        InstalledAtUtc = entity.InstalledAtUtc,
    };

    public static ModuleEntity ToEntity(this Module domain) => new()
    {
        Id = domain.Id,
        Name = domain.Name,
        Kind = domain.Kind,
        VersionMajor = domain.VersionMajor,
        VersionMinor = domain.VersionMinor,
        VersionPatch = domain.VersionPatch,
        CapabilitiesJson = domain.CapabilitiesJson,
        TestCommand = domain.TestCommand,
        SourceCodeRef = domain.SourceCodeRef,
        InstalledAtUtc = domain.InstalledAtUtc,
    };

    public static void ApplyTo(this Module domain, ModuleEntity entity)
    {
        entity.Name = domain.Name;
        entity.Kind = domain.Kind;
        entity.VersionMajor = domain.VersionMajor;
        entity.VersionMinor = domain.VersionMinor;
        entity.VersionPatch = domain.VersionPatch;
        entity.CapabilitiesJson = domain.CapabilitiesJson;
        entity.TestCommand = domain.TestCommand;
        entity.SourceCodeRef = domain.SourceCodeRef;
    }
}
