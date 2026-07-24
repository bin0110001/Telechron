namespace Telechron.Sdk.Storefront;

public sealed record StorefrontAcquisitionResult
{
    public required bool Success { get; init; }
    public required bool SignatureVerified { get; init; }
    public required bool ContainerSandboxPassed { get; init; }
    public string? InstalledModulePath { get; init; }
    public string? ErrorMessage { get; init; }
}

// R-MOD5b's sandboxed self-test (via the real IModuleTrustEvaluator) must
// run somewhere -- an Agent-owned container on a real Machine, using a
// real pinned Toolchain image. There is no way to run it without one, so
// the caller (whichever surface a human uses to acquire a Storefront
// module for a Project) supplies both, plus the Project's own
// already-approved capability set (R-MOD8: acquiring from Storefront is
// not itself an approval of the capabilities the module declares).
public sealed record StorefrontAcquisitionContext
{
    public required Guid TargetProjectId { get; init; }
    public required Guid TargetMachineId { get; init; }
    public required string ToolchainImageDigest { get; init; }
    public required IReadOnlyList<string> ProjectApprovedCapabilities { get; init; }
}

public interface IStorefrontCatalogService
{
    Task<IReadOnlyList<StorefrontModuleListing>> SearchCatalogAsync(string query, CancellationToken ct = default);

    Task<StorefrontAcquisitionResult> AcquireAndInstallModuleAsync(
        StorefrontModuleListing listing, StorefrontAcquisitionContext context, CancellationToken ct = default);
}
