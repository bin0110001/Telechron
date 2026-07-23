namespace Telechron.Host.DesignDocuments;

public static class DesignDocumentServiceCollectionExtensions
{
    public static IServiceCollection AddTelechronDesignDocuments(this IServiceCollection services)
    {
        services.AddScoped<ReflexiveDesignDocumentSeeder>();
        return services;
    }
}
