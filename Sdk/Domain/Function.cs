namespace Telechron.Sdk.Domain;

// A reusable operation contract exposed by a Module — the atom of a Workflow
// (R-DM4). Kind is free-form (not an enum) because new kinds arrive via
// modules at runtime and the domain can't enumerate them ahead of time.
// ModuleVersionMajor/Minor is the major.minor a Workflow step pinned at
// authoring time (R-DM7a); IsDeprecated lets an operation retire without
// breaking those pinned references.
public sealed record Function
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid ModuleId { get; init; }
    public required string Kind { get; init; }
    public required string InputArtifactTypesJson { get; init; }
    public required string OutputArtifactTypesJson { get; init; }
    public required bool IsDeprecated { get; init; }
    public required int ModuleVersionMajor { get; init; }
    public required int ModuleVersionMinor { get; init; }
}
