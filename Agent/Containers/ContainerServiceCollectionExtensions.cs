using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telechron.Sdk.Containers;

namespace Telechron.Agent.Containers;

public static class ContainerServiceCollectionExtensions
{
    public static IServiceCollection AddTelechronContainerExecution(
        this IServiceCollection services,
        Action<PodmanConnectionOptions>? configureConnection = null,
        Action<RegistryAllowlist>? configureAllowlist = null,
        Action<GpuTenancyPolicy>? configureGpuTenancy = null,
        Action<WarmContainerPoolOptions>? configureWarmPool = null)
    {
        services.Configure<PodmanConnectionOptions>(configureConnection ?? (_ => { }));
        services.Configure<RegistryAllowlist>(configureAllowlist ?? (_ => { }));
        // R-SYS8: default is "not a GPU Agent" -- an operator must
        // explicitly opt a machine into dedicated single-tenant GPU duty.
        services.Configure<GpuTenancyPolicy>(o =>
        {
            o.IsDedicatedGpuAgent = false;
            o.GpuDeviceIds = [];
            configureGpuTenancy?.Invoke(o);
        });
        services.Configure<WarmContainerPoolOptions>(configureWarmPool ?? (_ => { }));

        services.AddSingleton<IDockerClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PodmanConnectionOptions>>().Value;
            return new DockerClientConfiguration(new Uri(options.Endpoint)).CreateClient();
        });

        services.AddSingleton<IImageProvenanceVerifier, ImageProvenanceVerifier>();
        services.AddSingleton<IGpuCapabilityGate, UnimplementedGpuCapabilityGate>();
        services.AddSingleton<IGpuStateSanitizer, NvidiaSmiGpuStateSanitizer>();
        services.AddSingleton<IWarmContainerPool, WarmContainerPool>();
        services.AddSingleton<IContainerExecutionService, PodmanContainerExecutionService>();

        return services;
    }
}
