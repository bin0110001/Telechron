using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Telechron.Sdk.Modules;

namespace Telechron.Host.Synthesis;

public sealed class SynthesisIntegritySignerOptions
{
    public string KeyId { get; set; } = "telechron-synthesis";

    // Base64 ECDSA P-256 PKCS8 private key. If unset, an ephemeral
    // per-process key is generated (fine for tests/local dev; a real
    // deployment must configure this from its secret store so the
    // corresponding public key registered in TrustedPublisherKeyStore
    // stays valid across restarts).
    public string? PrivateKeyPkcs8Base64 { get; set; }
}

// R-MOD5a: distinct signing identity from RepairProvenanceSigner (R-SEC3)
// -- that one attests "this repair attempt happened and here's its
// forensic record"; this one is the supply-chain signature a
// Host-synthesized module needs to pass the SAME integrity check any
// third-party module would (no special-cased bypass for the Host's own
// output). The corresponding public key must be present in
// TrustedPublisherKeyStoreOptions.TrustedKeys under KeyId for
// IModuleIntegrityVerifier to ever accept it.
public sealed class SynthesisIntegritySigner
{
    private readonly ECDsa _ecdsa;
    private readonly string _keyId;

    public SynthesisIntegritySigner(IOptions<SynthesisIntegritySignerOptions> options)
    {
        var opts = options.Value;
        _keyId = opts.KeyId;
        _ecdsa = ECDsa.Create();

        if (opts.PrivateKeyPkcs8Base64 is not null)
            _ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(opts.PrivateKeyPkcs8Base64), out _);
        else
            _ecdsa.GenerateKey(ECCurve.NamedCurves.nistP256);
    }

    public string PublicKeySubjectPublicKeyInfoBase64 => Convert.ToBase64String(_ecdsa.ExportSubjectPublicKeyInfo());

    public ModuleIntegrityManifest Sign(byte[] assemblyBytes)
    {
        var checksum = Convert.ToHexStringLower(SHA256.HashData(assemblyBytes));
        var signature = _ecdsa.SignData(Convert.FromHexString(checksum), HashAlgorithmName.SHA256);
        return new ModuleIntegrityManifest(_keyId, checksum, Convert.ToBase64String(signature));
    }
}
