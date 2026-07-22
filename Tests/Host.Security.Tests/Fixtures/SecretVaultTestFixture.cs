using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Persistence;
using Telechron.Host.Security.Audit;
using Telechron.Sdk.Security;
using HostSecrets = Telechron.Host.Security.Secrets;
using HostLogging = Telechron.Host.Security.Logging;

namespace Telechron.Host.Security.Tests.Fixtures;

// Wires the real SecretVault against real (file-backed) TelechronDbContext +
// AuditDbContext, and a fixed test master key — end-to-end minus only the
// network/HTTP layer.
public sealed class SecretVaultTestFixture : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    public byte[] MasterKey { get; } = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);

    public SecretVaultTestFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "telechron-security-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var services = new ServiceCollection();
        services.AddTelechronPersistence(Path.Combine(root, "telechron.db"), Path.Combine(root, "backups"));
        services.AddTelechronAuditLog(Path.Combine(root, "audit.db"));
        services.AddSingleton<IMasterKeyProvider>(new FixedMasterKeyProvider(MasterKey));
        services.AddSingleton<ISecretEncryptionService, HostSecrets.AesGcmSecretEncryptionService>();
        services.AddSingleton<ISecretFingerprintRegistry, HostLogging.SecretFingerprintRegistry>();
        services.AddScoped<ISecretVault, HostSecrets.SecretVault>();
        services.AddScoped<ISecretResolutionScope, HostSecrets.SecretResolutionScope>();

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TelechronDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.Migrate();
    }

    public IServiceScope CreateScope() => _provider.CreateScope();

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    private sealed class FixedMasterKeyProvider(byte[] key) : IMasterKeyProvider
    {
        public string CurrentKeyId => "test-v1";
        public ReadOnlyMemory<byte> GetKey(string keyId) => keyId == CurrentKeyId ? key : throw new InvalidOperationException();
        public ReadOnlyMemory<byte> GetCurrentKey() => key;
    }
}
