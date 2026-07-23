using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules.Integrity;

// R-MOD5a: checksum guards against corruption/tampering-in-transit;
// signature (over the checksum bytes, by a key in TrustedPublisherKeyStore)
// proves provenance. Both must pass -- either alone is an incomplete
// supply-chain guarantee.
public sealed class ModuleIntegrityVerifier(TrustedPublisherKeyStore keyStore, ILogger<ModuleIntegrityVerifier> logger)
    : IModuleIntegrityVerifier
{
    public async Task<IntegrityVerificationResult> VerifyAsync(
        string moduleAssemblyPath, ModuleIntegrityManifest manifest, CancellationToken ct = default)
    {
        byte[] assemblyBytes;
        try
        {
            assemblyBytes = await File.ReadAllBytesAsync(moduleAssemblyPath, ct);
        }
        catch (IOException ex)
        {
            return new IntegrityVerificationResult(false, $"Could not read module assembly: {ex.Message}");
        }

        var actualChecksum = Convert.ToHexStringLower(SHA256.HashData(assemblyBytes));
        if (!string.Equals(actualChecksum, manifest.Sha256ChecksumHex, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Module integrity check failed: checksum mismatch for {Path}.", moduleAssemblyPath);
            return new IntegrityVerificationResult(false, "SHA-256 checksum does not match the manifest (R-MOD5a).");
        }

        using var publicKey = keyStore.GetPublicKey(manifest.PublisherKeyId);
        if (publicKey is null)
        {
            logger.LogWarning("Module integrity check failed: unknown publisher key '{KeyId}'.", manifest.PublisherKeyId);
            return new IntegrityVerificationResult(false, $"Publisher key '{manifest.PublisherKeyId}' is not trusted (R-MOD5a).");
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(manifest.SignatureBase64);
        }
        catch (FormatException)
        {
            return new IntegrityVerificationResult(false, "Signature is not valid base64.");
        }

        // Sign over the checksum bytes, not the (potentially huge) assembly
        // itself -- the checksum already binds to the exact assembly
        // content, and re-verifying it above means a signature-over-checksum
        // forgery would require both breaking SHA-256 and forging the
        // signature, not either alone.
        var checksumBytes = Convert.FromHexString(manifest.Sha256ChecksumHex);
        var signatureValid = publicKey.VerifyData(checksumBytes, signatureBytes, HashAlgorithmName.SHA256);

        if (!signatureValid)
        {
            logger.LogWarning("Module integrity check failed: signature verification failed for {Path}.", moduleAssemblyPath);
            return new IntegrityVerificationResult(false, "Signature verification failed (R-MOD5a).");
        }

        return new IntegrityVerificationResult(true, "Checksum and signature verified.");
    }
}
