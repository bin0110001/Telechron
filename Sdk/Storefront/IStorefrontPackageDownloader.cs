namespace Telechron.Sdk.Storefront;

// The one seam that reaches an external network address for Storefront
// acquisition (R-SYS5) -- separated from StorefrontCatalogService so tests
// can substitute a local fixture instead of a real HTTP endpoint, the same
// pattern Phase 6's ConnectorDispatcherTests used for GitHub.
public interface IStorefrontPackageDownloader
{
    // Downloads the package at DownloadUrl to a local temp file and
    // returns its path. Caller owns deleting the file when done with it.
    Task<string> DownloadToTempFileAsync(StorefrontModuleListing listing, CancellationToken ct = default);
}
