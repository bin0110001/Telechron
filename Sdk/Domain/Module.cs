namespace Telechron.Sdk.Domain;

// Hot-reloadable physical distribution unit (R-DM7). SourceCodeRef points to
// the module's source outside SQLite (same out-of-DB pattern as Artifact
// blobs, R-PER7) — R-MOD3 requires every module ship source, but the bytes
// don't belong in the operational DB. Version fields back R-DM7a semver
// compatibility rules (same-major hot-reloads transparently; differing major
// needs re-approval).
public sealed record Module
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required int VersionMajor { get; init; }
    public required int VersionMinor { get; init; }
    public required int VersionPatch { get; init; }
    public required string CapabilitiesJson { get; init; }
    public required string TestCommand { get; init; }
    public required string SourceCodeRef { get; init; }
    public required DateTimeOffset InstalledAtUtc { get; init; }
}
