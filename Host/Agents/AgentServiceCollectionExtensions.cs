using Telechron.Host.Agents.Dispatch;
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
        services.Configure<AgentEnrollmentOptions>(o => o.EnrollmentToken = enrollmentToken);
        return services;
    }
}
