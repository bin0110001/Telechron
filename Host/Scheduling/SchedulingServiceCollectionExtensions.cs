using Telechron.Sdk.Scheduling;

namespace Telechron.Host.Scheduling;

public static class SchedulingServiceCollectionExtensions
{
    // R-SCH1-5: without this registration, none of SchedulerService's
    // BackgroundService.ExecuteAsync loop, ResourceManager's exclusive-group
    // enforcement, or PriorityQueue's aging logic ever run in the shipped
    // Host -- they previously existed only as classes exercised by their
    // own unit tests.
    public static IServiceCollection AddTelechronScheduling(this IServiceCollection services)
    {
        services.AddSingleton<IResourceManager, ResourceManager>();
        services.AddSingleton(typeof(IPriorityQueue<>), typeof(PriorityQueue<>));
        services.AddSingleton<SchedulerService>();
        services.AddSingleton<ISchedulerService>(sp => sp.GetRequiredService<SchedulerService>());
        services.AddHostedService(sp => sp.GetRequiredService<SchedulerService>());
        return services;
    }
}
