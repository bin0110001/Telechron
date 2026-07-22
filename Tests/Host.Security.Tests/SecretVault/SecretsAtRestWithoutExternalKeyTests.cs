using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Persistence;
using Telechron.Host.Security.Tests.Fixtures;
using Telechron.Sdk.Security;

namespace Telechron.Host.Security.Tests.SecretVault;

// R-SEC9 done-when: "the DB file alone (without the external key) cannot
// decrypt them." Proves this by reading the raw ciphertext straight out of
// the DB (bypassing SecretVault entirely, as an attacker with filesystem
// access to the DB would) and confirming decryption fails without the KEK.
public sealed class SecretsAtRestWithoutExternalKeyTests : IAsyncLifetime
{
    private SecretVaultTestFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new SecretVaultTestFixture();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task RawCiphertext_FromDbAlone_DoesNotDecryptWithoutMasterKey()
    {
        using var scope = _fixture.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var projectId = await scope.SeedProjectAsync();
        var rawValue = "attacker-should-never-recover-this";
        var handle = await vault.StoreAsync(projectId, "Test Secret", Encoding.UTF8.GetBytes(rawValue));

        // Read the row directly, the way an attacker with only DB file access
        // would — no IMasterKeyProvider, no SecretVault, no application code.
        var db = scope.ServiceProvider.GetRequiredService<TelechronDbContext>();
        var row = await db.Secrets.AsNoTracking().FirstAsync(s => s.Handle == handle);
        var ciphertextAsText = Encoding.Latin1.GetString(row.EncryptedValue);

        Assert.DoesNotContain(rawValue, ciphertextAsText, StringComparison.Ordinal);

        // Decrypting with the wrong (attacker-guessed/absent) key must fail
        // rather than silently succeed or produce plausible-looking plaintext.
        var wrongKeyProvider = new FixedKeyProvider(new byte[32]); // all-zero key, not the fixture's real key
        var serviceWithWrongKey = new Host.Security.Secrets.AesGcmSecretEncryptionService(wrongKeyProvider);

        Assert.ThrowsAny<Exception>(() => serviceWithWrongKey.Decrypt(
            new EncryptedSecretValue(row.EncryptedValue, row.EncryptionKeyId)));
    }

    private sealed class FixedKeyProvider(byte[] key) : IMasterKeyProvider
    {
        public string CurrentKeyId => "test-v1";
        public ReadOnlyMemory<byte> GetKey(string keyId) => key;
        public ReadOnlyMemory<byte> GetCurrentKey() => key;
    }
}
