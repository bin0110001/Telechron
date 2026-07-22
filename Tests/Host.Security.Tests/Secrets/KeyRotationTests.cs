using System.Text;
using Telechron.Host.Security.Secrets;
using Telechron.Sdk.Security;

namespace Telechron.Host.Security.Tests.Secrets;

// R-SEC9: "support key rotation independent of secret rotation" — a secret
// encrypted under an older KEK version must remain decryptable after the
// active KEK rotates, as long as the retired key is still resolvable.
public sealed class KeyRotationTests
{
    private sealed class MultiVersionKeyProvider(string currentKeyId, Dictionary<string, byte[]> keysById) : IMasterKeyProvider
    {
        public string CurrentKeyId { get; } = currentKeyId;
        public ReadOnlyMemory<byte> GetKey(string keyId) =>
            keysById.TryGetValue(keyId, out var key) ? key : throw new InvalidOperationException($"Unknown key '{keyId}'.");
        public ReadOnlyMemory<byte> GetCurrentKey() => GetKey(CurrentKeyId);
    }

    [Fact]
    public void SecretEncryptedUnderRetiredKey_StillDecrypts_AfterKeyRotation()
    {
        var keyV1 = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var keyV2 = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);

        var providerAtV1 = new MultiVersionKeyProvider("v1", new Dictionary<string, byte[]> { ["v1"] = keyV1 });
        var serviceAtV1 = new AesGcmSecretEncryptionService(providerAtV1);
        var encrypted = serviceAtV1.Encrypt(Encoding.UTF8.GetBytes("value-from-before-rotation"));
        Assert.Equal("v1", encrypted.EncryptionKeyId);

        // Rotate: v2 becomes current, but v1 remains resolvable for old ciphertext.
        var providerAfterRotation = new MultiVersionKeyProvider(
            "v2", new Dictionary<string, byte[]> { ["v1"] = keyV1, ["v2"] = keyV2 });
        var serviceAfterRotation = new AesGcmSecretEncryptionService(providerAfterRotation);

        var decrypted = serviceAfterRotation.Decrypt(encrypted);

        Assert.Equal("value-from-before-rotation", Encoding.UTF8.GetString(decrypted));
    }

    [Fact]
    public void NewSecretsAfterRotation_UseTheNewKeyId()
    {
        var keyV1 = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var keyV2 = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var providerAfterRotation = new MultiVersionKeyProvider(
            "v2", new Dictionary<string, byte[]> { ["v1"] = keyV1, ["v2"] = keyV2 });
        var service = new AesGcmSecretEncryptionService(providerAfterRotation);

        var encrypted = service.Encrypt(Encoding.UTF8.GetBytes("new-value"));

        Assert.Equal("v2", encrypted.EncryptionKeyId);
    }
}
