using Telechron.Sdk.Security.Permissions;

namespace Telechron.Host.Security.Permissions;

public static class PermissionsServiceCollectionExtensions
{
    public static IServiceCollection AddTelechronPermissionMediation(this IServiceCollection services)
    {
        services.AddScoped<IPermissionMediator, PermissionMediator>();
        return services;
    }
}
