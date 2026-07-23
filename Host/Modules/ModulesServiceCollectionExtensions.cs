using Telechron.Host.Modules.Health;
using Telechron.Host.Modules.Integrity;
using Telechron.Host.Modules.Permissions;
using Telechron.Host.Modules.Runtime;
using Telechron.Host.Modules.SelfTest;
using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules;

public static class ModulesServiceCollectionExtensions
{
    public static IServiceCollection AddTelechronModules(
        this IServiceCollection services,
        Action<TrustedPublisherKeyStoreOptions>? configureTrustedKeys = null,
        Action<ModuleCanaryOptions>? configureCanary = null,
        Action<ModuleHealthMonitorOptions>? configureHealthMonitor = null)
    {
        services.Configure<TrustedPublisherKeyStoreOptions>(configureTrustedKeys ?? (_ => { }));
        services.Configure<ModuleCanaryOptions>(configureCanary ?? (_ => { }));
        services.Configure<ModuleHealthMonitorOptions>(configureHealthMonitor ?? (_ => { }));
        services.AddSingleton<TrustedPublisherKeyStore>();
        services.AddSingleton<IModuleIntegrityVerifier, ModuleIntegrityVerifier>();
        services.AddSingleton<IModuleRuntime, ModuleRuntime>();
        services.AddSingleton<IContainerizedModuleSelfTestRunner, ContainerizedModuleSelfTestRunner>();
        services.AddSingleton<ISelfTestFalsifiabilityChecker, SelfTestFalsifiabilityChecker>();
        services.AddSingleton<IModuleCapabilityMediator, ModuleCapabilityMediator>();
        services.AddSingleton<IModuleTrustEvaluator, ModuleTrustEvaluator>();
        services.AddSingleton<InFlightInvocationTracker>();
        services.AddSingleton<IModuleDrainCoordinator, ModuleDrainCoordinator>();
        services.AddSingleton<IModuleCanaryObserver, ModuleCanaryObserver>();
        services.AddSingleton<IModuleHotReloadCoordinator, ModuleHotReloadCoordinator>();
        services.AddSingleton<IModuleHealthMonitor, ModuleHealthMonitor>();
        return services;
    }
}
