using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telechron.Sdk.Containers;

namespace Telechron.Agent.Containers;

public static class ContainerServiceCollectionExtensions
{
    public static IServiceCollection AddTelechronContainerExecution(
        this IServiceCollection services, Action<PodmanConnectionOptions>? configureConnection = null, Action<RegistryAllowlist>? configureAllowlist = null)
    {
        services.Configure<PodmanConnectionOptions>(configureConnection ?? (_ => { }));
        services.Configure<RegistryAllowlist>(configureAllowlist ?? (_ => { }));

        services.AddSingleton<IDockerClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PodmanConnectionOptions>>().Value;
            return new DockerClientConfiguration(new Uri(options.Endpoint)).CreateClient();
        });

        services.AddSingleton<IImageProvenanceVerifier, ImageProvenanceVerifier>();
        services.AddSingleton<IContainerExecutionService, PodmanContainerExecutionService>();

        return services;
    }
}
