using Telechron.Sdk.Reliability;

namespace Telechron.Host.Reliability;

public static class ReliabilityServiceCollectionExtensions
{
    // R-REL3/R-REL4: HostSentinel and HostScalingMonitor previously existed
    // only as classes exercised by their own unit tests -- neither was
    // registered anywhere the running Host would ever construct or call
    // them.
    public static IServiceCollection AddTelechronReliability(this IServiceCollection services)
    {
        services.AddScoped<IHostSentinel, HostSentinel>();
        services.AddScoped<IHostScalingMonitor, HostScalingMonitor>();
        return services;
    }
}
