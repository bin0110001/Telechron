namespace Telechron.Sdk.Storefront;

public sealed record StorefrontModuleListing
{
    public required string ModuleId { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public required string PublisherId { get; init; }

    // Must match a KeyId already present in TrustedPublisherKeyStoreOptions.TrustedKeys
    // (R-MOD5a) -- a listing naming an untrusted/unknown key fails integrity
    // verification, same as any other module install.
    public required string PublisherKeyId { get; init; }
    public required string PackageSha256Checksum { get; init; }

    // Base64 ECDSA signature over the SHA-256 checksum bytes, matching
    // ModuleIntegrityManifest.SignatureBase64 (R-MOD5a) -- reuses Phase 5's
    // real verifier rather than a parallel format.
    public required string SignatureBase64 { get; init; }
    public required IReadOnlyList<string> RequestedCapabilities { get; init; }
    public required string DownloadUrl { get; init; }
}
