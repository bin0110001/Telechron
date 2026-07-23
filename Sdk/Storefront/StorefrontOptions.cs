namespace Telechron.Sdk.Storefront;

public sealed record StorefrontOptions
{
    // R-SYS5: Storefront catalog is disabled by default
    public bool Enabled { get; init; } = false;
    public string CatalogBaseUrl { get; init; } = "https://storefront.telechron.io/api/v1";
    public bool RequirePublisherSignature { get; init; } = true;
    public bool EnforcePreTrustContainerSandbox { get; init; } = true;
}
