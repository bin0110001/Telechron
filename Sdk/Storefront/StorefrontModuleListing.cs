namespace Telechron.Sdk.Storefront;

public sealed record StorefrontModuleListing
{
    public required string ModuleId { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public required string PublisherId { get; init; }
    public required string PublisherPublicKeyPem { get; init; }
    public required string PackageSha256Checksum { get; init; }
    public required string SignatureHex { get; init; }
    public required IReadOnlyList<string> RequestedCapabilities { get; init; }
    public required string DownloadUrl { get; init; }
}
