namespace Telechron.Host.Llm;

public static class LlmServiceCollectionExtensions
{
    public static IServiceCollection AddTelechronLlm(
        this IServiceCollection services,
        Action<LlmProviderRegistryOptions>? configureProviders = null,
        Action<SpendCapOptions>? configureSpendCaps = null,
        Action<LlmCostEstimatorOptions>? configureCostEstimator = null)
    {
        services.Configure<LlmProviderRegistryOptions>(configureProviders ?? (_ => { }));
        services.Configure<SpendCapOptions>(configureSpendCaps ?? (_ => { }));
        services.Configure<LlmCostEstimatorOptions>(configureCostEstimator ?? (_ => { }));

        services.AddSingleton<ILlmProviderRegistry, LlmProviderRegistry>();
        services.AddScoped<ISpendCapEnforcer, SpendCapEnforcer>();
        services.AddSingleton<ILlmCostEstimator, LlmCostEstimator>();
        services.AddScoped<ILlmDispatcher, LlmDispatcher>();

        return services;
    }
}
