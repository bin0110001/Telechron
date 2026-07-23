using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telechron.Host.Modules.Integrity;
using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules.Tests;

public class ModuleIntegrityVerifierTests : IDisposable
{
    private readonly string _assemblyPath;
    private readonly ECDsa _publisherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public ModuleIntegrityVerifierTests()
    {
        _assemblyPath = Path.Combine(AppContext.BaseDirectory, "Telechron.Modules.Sample.dll");
    }

    public void Dispose() => _publisherKey.Dispose();

    private ModuleIntegrityVerifier CreateVerifier(string keyId = "test-publisher")
    {
        var publicKeyBase64 = Convert.ToBase64String(_publisherKey.ExportSubjectPublicKeyInfo());
        var options = Options.Create(new TrustedPublisherKeyStoreOptions
        {
            TrustedKeys = { [keyId] = publicKeyBase64 },
        });
        return new ModuleIntegrityVerifier(new TrustedPublisherKeyStore(options), NullLogger<ModuleIntegrityVerifier>.Instance);
    }

    private ModuleIntegrityManifest SignManifest(string keyId = "test-publisher")
    {
        var checksum = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(_assemblyPath)));
        var signature = _publisherKey.SignData(Convert.FromHexString(checksum), HashAlgorithmName.SHA256);
        return new ModuleIntegrityManifest(keyId, checksum, Convert.ToBase64String(signature));
    }

    [Fact]
    public async Task VerifyAsync_ValidChecksumAndSignature_IsValid()
    {
        var verifier = CreateVerifier();
        var manifest = SignManifest();

        var result = await verifier.VerifyAsync(_assemblyPath, manifest);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_TamperedChecksum_IsRejected()
    {
        var verifier = CreateVerifier();
        var manifest = SignManifest() with { Sha256ChecksumHex = new string('0', 64) };

        var result = await verifier.VerifyAsync(_assemblyPath, manifest);

        Assert.False(result.IsValid);
        Assert.Contains("checksum", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyAsync_UnknownPublisherKey_IsRejected()
    {
        var verifier = CreateVerifier(keyId: "trusted-key");
        var manifest = SignManifest(keyId: "unknown-key");

        var result = await verifier.VerifyAsync(_assemblyPath, manifest);

        Assert.False(result.IsValid);
        Assert.Contains("not trusted", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyAsync_SignatureFromWrongKey_IsRejected()
    {
        var verifier = CreateVerifier();
        var manifest = SignManifest();

        // Sign with a different key than the one the store trusts under
        // the same KeyId -- simulates a forged signature.
        using var attackerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var forgedSignature = attackerKey.SignData(Convert.FromHexString(manifest.Sha256ChecksumHex), HashAlgorithmName.SHA256);
        var forgedManifest = manifest with { SignatureBase64 = Convert.ToBase64String(forgedSignature) };

        var result = await verifier.VerifyAsync(_assemblyPath, forgedManifest);

        Assert.False(result.IsValid);
        Assert.Contains("signature", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyAsync_MalformedSignatureBase64_IsRejectedNotThrown()
    {
        var verifier = CreateVerifier();
        var manifest = SignManifest() with { SignatureBase64 = "not-valid-base64!!!" };

        var result = await verifier.VerifyAsync(_assemblyPath, manifest);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_MissingAssembly_IsRejectedNotThrown()
    {
        var verifier = CreateVerifier();
        var manifest = SignManifest();

        var result = await verifier.VerifyAsync(Path.Combine(AppContext.BaseDirectory, "does-not-exist.dll"), manifest);

        Assert.False(result.IsValid);
    }
}
