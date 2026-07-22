using Telechron.Host.Security.Logging;
using Telechron.Sdk.Security;

namespace Telechron.Host.Security.Secrets;

public static class SecretsServiceCollectionExtensions
{
    public static IServiceCollection AddTelechronSecretVault(this IServiceCollection services)
    {
        services.AddSingleton<IMasterKeyProvider, EnvironmentMasterKeyProvider>();
        services.AddSingleton<ISecretEncryptionService, AesGcmSecretEncryptionService>();
        services.AddSingleton<ISecretFingerprintRegistry, SecretFingerprintRegistry>();
        services.AddScoped<ISecretVault, SecretVault>();
        services.AddScoped<ISecretResolutionScope, SecretResolutionScope>();
        return services;
    }
}
