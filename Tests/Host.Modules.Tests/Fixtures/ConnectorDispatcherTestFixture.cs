using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Connectors;
using Telechron.Host.Modules;
using Telechron.Host.Modules.Integrity;
using Telechron.Host.Modules.Permissions;
using Telechron.Host.Modules.Runtime;
using Telechron.Host.Persistence;
using Telechron.Host.Security.Audit;
using Telechron.Host.Security.Permissions;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Security;
using Telechron.Sdk.Security.Permissions;
using HostSecrets = Telechron.Host.Security.Secrets;
using HostLogging = Telechron.Host.Security.Logging;

namespace Telechron.Host.Modules.Tests.Fixtures;

// Wires ConnectorDispatcher against real components throughout: real
// SecretVault (file-backed SQLite + a fixed test master key, matching
// Host.Security.Tests' own SecretVaultTestFixture pattern), real
// PermissionMediator/ModuleCapabilityMediator, real ModuleRuntime. Only
// the module assembly and the HTTP transport (per GitHubConnectorModuleTests)
// are test-controlled.
public sealed class ConnectorDispatcherTestFixture : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public ConnectorDispatcherTestFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "telechron-connector-dispatcher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var masterKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);

        var services = new ServiceCollection();
        services.AddTelechronPersistence(Path.Combine(root, "telechron.db"), Path.Combine(root, "backups"));
        services.AddTelechronAuditLog(Path.Combine(root, "audit.db"));
        services.AddSingleton<IMasterKeyProvider>(new FixedMasterKeyProvider(masterKey));
        services.AddSingleton<ISecretEncryptionService, HostSecrets.AesGcmSecretEncryptionService>();
        services.AddSingleton<ISecretFingerprintRegistry, HostLogging.SecretFingerprintRegistry>();
        services.AddScoped<ISecretVault, HostSecrets.SecretVault>();
        services.AddScoped<ISecretResolutionScope, HostSecrets.SecretResolutionScope>();
        services.AddScoped<IPermissionMediator, PermissionMediator>();
        services.AddSingleton<IModuleCapabilityMediator, ModuleCapabilityMediator>();
        services.AddSingleton<IModuleRuntime, ModuleRuntime>();
        services.AddScoped<IConnectorDispatcher, ConnectorDispatcher>();
        services.AddLogging();

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
