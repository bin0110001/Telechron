using Telechron.Host.DesignDocs;

namespace Telechron.Host.DesignDocuments;

public static class DesignDocumentServiceCollectionExtensions
{
    public static IServiceCollection AddTelechronDesignDocuments(this IServiceCollection services)
    {
        services.AddScoped<ReflexiveDesignDocumentSeeder>();
        services.AddScoped<ReflexiveDesignDocWire>();
        return services;
    }
}
