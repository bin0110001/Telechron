using Telechron.Host.Agents.Dispatch;
using Telechron.Host.Agents.Watchdog;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Security;

namespace Telechron.Host.Agents;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddTelechronAgentGrpc(this IServiceCollection services, string enrollmentToken)
    {
        services.AddGrpc();
        services.AddSingleton<ICommandDispatchValidator, CommandDispatchValidator>();
        services.AddSingleton<IDispatchQueue, InMemoryDispatchQueue>();
        services.AddSingleton<ICommandResultCorrelator, InMemoryCommandResultCorrelator>();
        services.Configure<AgentEnrollmentOptions>(o => o.EnrollmentToken = enrollmentToken);
        return services;
    }

    // R-REL1/R-SCH5: stalled-run watchdog with a bounded grace/reconnect
    // window. Scoped pass + hosted-service wrapper, same shape as
    // ScheduledRetentionHostedService/RetentionPass.
    public static IServiceCollection AddTelechronStalledRunWatchdog(
        this IServiceCollection services, Action<StalledRunWatchdogOptions>? configure = null)
    {
        services.Configure<StalledRunWatchdogOptions>(configure ?? (_ => { }));
        services.AddScoped<StalledRunWatchdogPass>();
        services.AddHostedService<ScheduledStalledRunWatchdogHostedService>();
        return services;
    }
}
