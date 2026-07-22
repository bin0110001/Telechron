using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class FunctionMapper
{
    public static Function ToDomain(this FunctionEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        ModuleId = entity.ModuleId,
        Kind = entity.Kind,
        InputArtifactTypesJson = entity.InputArtifactTypesJson,
        OutputArtifactTypesJson = entity.OutputArtifactTypesJson,
        IsDeprecated = entity.IsDeprecated,
        ModuleVersionMajor = entity.ModuleVersionMajor,
        ModuleVersionMinor = entity.ModuleVersionMinor,
    };

    public static FunctionEntity ToEntity(this Function domain) => new()
    {
        Id = domain.Id,
        Name = domain.Name,
        ModuleId = domain.ModuleId,
        Kind = domain.Kind,
        InputArtifactTypesJson = domain.InputArtifactTypesJson,
        OutputArtifactTypesJson = domain.OutputArtifactTypesJson,
        IsDeprecated = domain.IsDeprecated,
        ModuleVersionMajor = domain.ModuleVersionMajor,
        ModuleVersionMinor = domain.ModuleVersionMinor,
    };

    public static void ApplyTo(this Function domain, FunctionEntity entity)
    {
        entity.Name = domain.Name;
        entity.ModuleId = domain.ModuleId;
        entity.Kind = domain.Kind;
        entity.InputArtifactTypesJson = domain.InputArtifactTypesJson;
        entity.OutputArtifactTypesJson = domain.OutputArtifactTypesJson;
        entity.IsDeprecated = domain.IsDeprecated;
        entity.ModuleVersionMajor = domain.ModuleVersionMajor;
        entity.ModuleVersionMinor = domain.ModuleVersionMinor;
    }
}
