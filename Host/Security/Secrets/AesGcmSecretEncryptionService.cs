using System.Security.Cryptography;
using Telechron.Sdk.Security;

namespace Telechron.Host.Security.Secrets;

// R-SEC1/R-SEC9: AES-256-GCM envelope encryption. Ciphertext binary layout
// (all lengths fixed except the wrapped DEK, which AES-GCM keeps at DEK size):
//   [1 byte format version]
//   [12 bytes DEK-wrap nonce][16 bytes DEK-wrap tag][32 bytes wrapped DEK]
//   [12 bytes value nonce][16 bytes value tag][N bytes value ciphertext]
// The wrapped DEK is stored in the same blob as the value ciphertext (both
// live in Secret.EncryptedValue); EncryptionKeyId records which KEK wrapped
// the DEK, resolved via IMasterKeyProvider at decrypt time — enabling KEK
// rotation without touching the DEK-wrapped value.
public sealed class AesGcmSecretEncryptionService(IMasterKeyProvider masterKeyProvider) : ISecretEncryptionService
{
    private const byte FormatVersion = 1;
    private const int DekSizeBytes = 32; // AES-256
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    public EncryptedSecretValue Encrypt(ReadOnlySpan<byte> plaintext)
    {
        var dek = RandomNumberGenerator.GetBytes(DekSizeBytes);
        try
        {
            var valueNonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var valueTag = new byte[TagSizeBytes];
            var valueCiphertext = new byte[plaintext.Length];
            using (var valueCipher = new AesGcm(dek, TagSizeBytes))
            {
                valueCipher.Encrypt(valueNonce, plaintext, valueCiphertext, valueTag);
            }

            var keyId = masterKeyProvider.CurrentKeyId;
            var kek = masterKeyProvider.GetCurrentKey();
            var dekWrapNonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var dekWrapTag = new byte[TagSizeBytes];
            var wrappedDek = new byte[DekSizeBytes];
            using (var kekCipher = new AesGcm(kek.Span, TagSizeBytes))
            {
                kekCipher.Encrypt(dekWrapNonce, dek, wrappedDek, dekWrapTag);
            }

            var blob = new byte[1 + NonceSizeBytes + TagSizeBytes + DekSizeBytes + NonceSizeBytes + TagSizeBytes + valueCiphertext.Length];
            var offset = 0;
            blob[offset++] = FormatVersion;
            dekWrapNonce.CopyTo(blob, offset); offset += NonceSizeBytes;
            dekWrapTag.CopyTo(blob, offset); offset += TagSizeBytes;
            wrappedDek.CopyTo(blob, offset); offset += DekSizeBytes;
            valueNonce.CopyTo(blob, offset); offset += NonceSizeBytes;
            valueTag.CopyTo(blob, offset); offset += TagSizeBytes;
            valueCiphertext.CopyTo(blob, offset);

            return new EncryptedSecretValue(blob, keyId);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    public byte[] Decrypt(EncryptedSecretValue encrypted)
    {
        var blob = encrypted.Ciphertext;
        if (blob.Length < 1 + (NonceSizeBytes + TagSizeBytes) * 2 + DekSizeBytes)
            throw new CryptographicException("Encrypted secret blob is malformed (too short).");
        if (blob[0] != FormatVersion)
            throw new CryptographicException($"Unsupported encrypted secret format version {blob[0]}.");

        var offset = 1;
        var dekWrapNonce = blob.AsSpan(offset, NonceSizeBytes); offset += NonceSizeBytes;
        var dekWrapTag = blob.AsSpan(offset, TagSizeBytes); offset += TagSizeBytes;
        var wrappedDek = blob.AsSpan(offset, DekSizeBytes); offset += DekSizeBytes;
        var valueNonce = blob.AsSpan(offset, NonceSizeBytes); offset += NonceSizeBytes;
        var valueTag = blob.AsSpan(offset, TagSizeBytes); offset += TagSizeBytes;
        var valueCiphertext = blob.AsSpan(offset);

        var kek = masterKeyProvider.GetKey(encrypted.EncryptionKeyId);
        var dek = new byte[DekSizeBytes];
        try
        {
            using (var kekCipher = new AesGcm(kek.Span, TagSizeBytes))
            {
                kekCipher.Decrypt(dekWrapNonce, wrappedDek, dekWrapTag, dek);
            }

            var plaintext = new byte[valueCiphertext.Length];
            using var valueCipher = new AesGcm(dek, TagSizeBytes);
            valueCipher.Decrypt(valueNonce, valueCiphertext, valueTag, plaintext);
            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }
}
