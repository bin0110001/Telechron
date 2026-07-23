using Telechron.Host.Modules.Integrity;
using Telechron.Host.Modules.Runtime;
using Telechron.Host.Modules.SelfTest;
using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules;

public static class ModulesServiceCollectionExtensions
{
    public static IServiceCollection AddTelechronModules(
        this IServiceCollection services, Action<TrustedPublisherKeyStoreOptions>? configureTrustedKeys = null)
    {
        services.Configure<TrustedPublisherKeyStoreOptions>(configureTrustedKeys ?? (_ => { }));
        services.AddSingleton<TrustedPublisherKeyStore>();
        services.AddSingleton<IModuleIntegrityVerifier, ModuleIntegrityVerifier>();
        services.AddSingleton<IModuleRuntime, ModuleRuntime>();
        services.AddSingleton<ISelfTestFalsifiabilityChecker, SelfTestFalsifiabilityChecker>();
        return services;
    }
}
