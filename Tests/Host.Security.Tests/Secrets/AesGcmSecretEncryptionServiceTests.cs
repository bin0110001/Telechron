using System.Text;
using Telechron.Host.Security.Secrets;
using Telechron.Sdk.Security;

namespace Telechron.Host.Security.Tests.Secrets;

public sealed class AesGcmSecretEncryptionServiceTests
{
    private sealed class FakeMasterKeyProvider(byte[] key, string keyId = "v1") : IMasterKeyProvider
    {
        public string CurrentKeyId { get; } = keyId;
        public ReadOnlyMemory<byte> GetKey(string keyId) =>
            keyId == CurrentKeyId ? key : throw new InvalidOperationException("Unknown key id.");
        public ReadOnlyMemory<byte> GetCurrentKey() => key;
    }

    private static byte[] NewKey() => System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void EncryptThenDecrypt_RoundTrips()
    {
        var service = new AesGcmSecretEncryptionService(new FakeMasterKeyProvider(NewKey()));
        var plaintext = Encoding.UTF8.GetBytes("ghp_super_secret_token_value");

        var encrypted = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(encrypted);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_NeverProducesPlaintextSubstringInCiphertext()
    {
        var service = new AesGcmSecretEncryptionService(new FakeMasterKeyProvider(NewKey()));
        var plaintext = Encoding.UTF8.GetBytes("extremely-recognizable-secret-value-12345");

        var encrypted = service.Encrypt(plaintext);
        var ciphertextAsLatin1 = Encoding.Latin1.GetString(encrypted.Ciphertext);

        Assert.DoesNotContain("extremely-recognizable-secret-value-12345", ciphertextAsLatin1);
    }

    [Fact]
    public void Decrypt_WithWrongKey_Throws()
    {
        var service = new AesGcmSecretEncryptionService(new FakeMasterKeyProvider(NewKey()));
        var encrypted = service.Encrypt(Encoding.UTF8.GetBytes("value"));

        var wrongKeyService = new AesGcmSecretEncryptionService(new FakeMasterKeyProvider(NewKey()));

        Assert.ThrowsAny<Exception>(() => wrongKeyService.Decrypt(encrypted));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsRatherThanReturningWrongPlaintext()
    {
        var service = new AesGcmSecretEncryptionService(new FakeMasterKeyProvider(NewKey()));
        var encrypted = service.Encrypt(Encoding.UTF8.GetBytes("value"));
        encrypted.Ciphertext[^1] ^= 0xFF; // flip last byte of the value ciphertext

        Assert.ThrowsAny<Exception>(() => service.Decrypt(encrypted));
    }

    [Fact]
    public void Encrypt_SameValueTwice_ProducesDifferentCiphertext()
    {
        // Random nonce/DEK per call — critical for semantic security (identical
        // secrets must not be distinguishable by ciphertext comparison).
        var service = new AesGcmSecretEncryptionService(new FakeMasterKeyProvider(NewKey()));
        var plaintext = Encoding.UTF8.GetBytes("same-value");

        var a = service.Encrypt(plaintext);
        var b = service.Encrypt(plaintext);

        Assert.NotEqual(a.Ciphertext, b.Ciphertext);
    }
}
