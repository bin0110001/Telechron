using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Llm;
using Telechron.Host.Modules.Runtime;
using Telechron.Host.Persistence;
using Telechron.Host.Security.Audit;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Security;
using HostSecrets = Telechron.Host.Security.Secrets;
using HostLogging = Telechron.Host.Security.Logging;

namespace Telechron.Host.Modules.Tests.Fixtures;

// Wires LlmDispatcher against real components: real (file-backed SQLite)
// ILlmCallRepository, real SpendCapEnforcer reading from it, real
// SecretVault/SecretResolutionScope (matching Host.Security.Tests'
// SecretVaultTestFixture pattern), real ModuleRuntime for provider
// resolution. Only the specific engine module instance and its HTTP
// target are test-controlled.
public sealed class LlmDispatcherTestFixture : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public LlmDispatcherTestFixture(
        Action<LlmProviderRegistryOptions>? configureProviders = null,
        Action<SpendCapOptions>? configureSpendCaps = null,
        Action<LlmCostEstimatorOptions>? configureCosts = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "telechron-llm-dispatcher-tests", Guid.NewGuid().ToString("N"));
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
        services.AddSingleton<IModuleRuntime, ModuleRuntime>();
        services.AddTelechronLlm(
            configureProviders: configureProviders, configureSpendCaps: configureSpendCaps, configureCostEstimator: configureCosts);
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
