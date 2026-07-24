using Telechron.Sdk.Storefront;

namespace Telechron.Host.Storefront;

public sealed class HttpStorefrontPackageDownloader(IHttpClientFactory httpClientFactory) : IStorefrontPackageDownloader
{
    public async Task<string> DownloadToTempFileAsync(StorefrontModuleListing listing, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient(nameof(HttpStorefrontPackageDownloader));
        using var response = await client.GetAsync(listing.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var tempPath = Path.Combine(Path.GetTempPath(), $"telechron-storefront-{Guid.NewGuid():N}.pkg");
        await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fileStream, ct);
        return tempPath;
    }
}
