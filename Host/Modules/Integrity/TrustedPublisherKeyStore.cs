using System.Security.Cryptography;

namespace Telechron.Host.Modules.Integrity;

// R-MOD5a: the set of publisher keys the Host will accept a module
// signature from. ECDSA P-256 public keys, base64-encoded X.509
// SubjectPublicKeyInfo -- deliberately simple (a config-driven allowlist)
// rather than a full PKI/CA chain, since R-MOD5a asks for "a known
// publisher key," not third-party-issued certificates.
public sealed class TrustedPublisherKeyStoreOptions
{
    // KeyId -> base64 SubjectPublicKeyInfo (ECDSA P-256).
    public Dictionary<string, string> TrustedKeys { get; set; } = [];
}

public sealed class TrustedPublisherKeyStore(Microsoft.Extensions.Options.IOptions<TrustedPublisherKeyStoreOptions> options)
{
    public ECDsa? GetPublicKey(string publisherKeyId)
    {
        if (!options.Value.TrustedKeys.TryGetValue(publisherKeyId, out var base64SubjectPublicKeyInfo))
            return null;

        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(base64SubjectPublicKeyInfo), out _);
            return ecdsa;
        }
        catch
        {
            ecdsa.Dispose();
            return null;
        }
    }
}
