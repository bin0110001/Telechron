namespace Telechron.Sdk.Storefront;

public sealed record StorefrontOptions
{
    // R-SYS5: Storefront catalog is disabled by default
    public bool Enabled { get; set; } = false;
    public string CatalogBaseUrl { get; set; } = "https://storefront.telechron.io/api/v1";
    public bool RequirePublisherSignature { get; set; } = true;
    public bool EnforcePreTrustContainerSandbox { get; set; } = true;
}
