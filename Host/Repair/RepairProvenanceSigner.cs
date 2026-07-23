using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Telechron.Sdk.Repair;

namespace Telechron.Host.Repair;

// Default R-SEC3 implementation: ECDSA P-256, signing the canonical JSON
// serialization of the RepairProvenanceRecord (deterministic property
// order via source-generated-free System.Text.Json default reflection
// serializer is stable within one process/runtime -- adequate here since
// sign and verify both happen in this same process against the same
// record shape, not across a wire format needing cross-language stability).
public sealed class RepairProvenanceSigner : IRepairProvenanceSigner
{
    private readonly ECDsa _ecdsa;
    private readonly string _keyId;

    public RepairProvenanceSigner(IOptions<RepairProvenanceSignerOptions> options)
    {
        var opts = options.Value;
        _keyId = opts.KeyId;
        _ecdsa = ECDsa.Create();

        if (opts.PrivateKeyPkcs8Base64 is not null)
        {
            _ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(opts.PrivateKeyPkcs8Base64), out _);
        }
        else if (opts.PublicKeySubjectPublicKeyInfoBase64 is not null)
        {
            // Verify-only configuration (e.g. a Host instance that checks
            // provenance but never itself commits repairs).
            _ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(opts.PublicKeySubjectPublicKeyInfoBase64), out _);
        }
        else
        {
            // No configured key material -- generate an ephemeral P-256
            // keypair for this process. Sign+verify both work within the
            // process lifetime (tests, local dev); a real deployment must
            // configure PrivateKeyPkcs8Base64 from its secret store so
            // provenance signatures remain verifiable across restarts.
            _ecdsa.GenerateKey(ECCurve.NamedCurves.nistP256);
        }
    }

    public SignedRepairProvenance Sign(RepairProvenanceRecord record)
    {
        var canonicalBytes = CanonicalBytes(record);
        var signature = _ecdsa.SignData(canonicalBytes, HashAlgorithmName.SHA256);
        return new SignedRepairProvenance(record, Convert.ToBase64String(signature), _keyId);
    }

    public bool Verify(SignedRepairProvenance signed)
    {
        if (!string.Equals(signed.SignerKeyId, _keyId, StringComparison.Ordinal))
            return false;

        var canonicalBytes = CanonicalBytes(signed.Record);
        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(signed.SignatureBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        return _ecdsa.VerifyData(canonicalBytes, signatureBytes, HashAlgorithmName.SHA256);
    }

    private static byte[] CanonicalBytes(RepairProvenanceRecord record) =>
        JsonSerializer.SerializeToUtf8Bytes(record);
}
