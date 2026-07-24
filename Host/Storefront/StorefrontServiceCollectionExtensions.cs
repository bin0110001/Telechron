using Telechron.Sdk.Storefront;

namespace Telechron.Host.Storefront;

public static class StorefrontServiceCollectionExtensions
{
    // R-SYS5: registered only where its real dependencies (IModuleIntegrityVerifier,
    // IModuleTrustEvaluator, IModuleRuntime -- see AddTelechronModules) already are;
    // Storefront acquisition cannot function without a live Agent transport to run
    // the R-MOD5b pre-trust sandboxed self-test against, same as any other module.
    public static IServiceCollection AddTelechronStorefront(
        this IServiceCollection services, Action<StorefrontOptions>? configure = null)
    {
        services.Configure<StorefrontOptions>(configure ?? (_ => { }));
        services.AddHttpClient(nameof(HttpStorefrontPackageDownloader));
        services.AddSingleton<IStorefrontPackageDownloader, HttpStorefrontPackageDownloader>();
        services.AddSingleton<IStorefrontCatalogService, StorefrontCatalogService>();
        return services;
    }
}
