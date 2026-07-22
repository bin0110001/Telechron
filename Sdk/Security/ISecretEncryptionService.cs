namespace Telechron.Sdk.Security;

public sealed record EncryptedSecretValue(byte[] Ciphertext, string EncryptionKeyId);

// R-SEC1/R-SEC9: envelope encryption for Secret values. Each secret gets its
// own random data-encryption key (DEK); the DEK is wrapped with the current
// master key (KEK, via IMasterKeyProvider) and stored alongside the ciphertext
// — the KEK itself never touches SQLite. Rotating the KEK only requires
// rewrapping DEKs, never re-encrypting secret values.
public interface ISecretEncryptionService
{
    EncryptedSecretValue Encrypt(ReadOnlySpan<byte> plaintext);

    byte[] Decrypt(EncryptedSecretValue encrypted);
}
