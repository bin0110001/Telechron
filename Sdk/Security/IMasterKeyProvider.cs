namespace Telechron.Sdk.Security;

// R-SEC9: resolves the current master key (KEK) used to wrap per-secret DEKs.
// Implementations source this from a platform key store, HSM, or an externally
// supplied master key — never from the same persistence store as the secrets
// it protects (SQLite/R-PER1). KeyId identifies which KEK version is active,
// enabling rotation (R-SEC9) without touching ciphertext.
public interface IMasterKeyProvider
{
    string CurrentKeyId { get; }

    ReadOnlyMemory<byte> GetKey(string keyId);

    ReadOnlyMemory<byte> GetCurrentKey();
}
