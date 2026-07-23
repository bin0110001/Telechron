namespace Telechron.Sdk.Storefront;

public sealed record StorefrontAcquisitionResult
{
    public required bool Success { get; init; }
    public required bool SignatureVerified { get; init; }
    public required bool ContainerSandboxPassed { get; init; }
    public string? InstalledModulePath { get; init; }
    public string? ErrorMessage { get; init; }
}

public interface IStorefrontCatalogService
{
    Task<IReadOnlyList<StorefrontModuleListing>> SearchCatalogAsync(string query, CancellationToken ct = default);
    Task<StorefrontAcquisitionResult> AcquireAndInstallModuleAsync(StorefrontModuleListing listing, Guid targetProjectId, CancellationToken ct = default);
}
