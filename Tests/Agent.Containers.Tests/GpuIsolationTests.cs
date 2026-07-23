using Docker.DotNet;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telechron.Agent.Containers;
using Telechron.Sdk.Containers;

namespace Telechron.Agent.Containers.Tests;

// R-SYS8: proves the refusal path -- no GPU work reaches Podman unless this
// Agent is configured as a dedicated single-tenant GPU Agent AND capability
// approval succeeds. No real Docker calls happen in these tests: refusal
// must occur before dockerClient is ever touched, so a null client would
// throw if the refusal path were bypassed -- these tests rely on that by
// using a client that isn't wired to a real Podman machine at all.
public class GpuIsolationTests
{
    private static PodmanContainerExecutionService CreateService(
        bool isDedicatedGpuAgent, IReadOnlyList<string>? gpuDeviceIds = null, IGpuCapabilityGate? gate = null)
    {
        var dockerClient = new DockerClientConfiguration(new Uri("npipe://./pipe/telechron-unused-in-these-tests")).CreateClient();
        var policy = Options.Create(new GpuTenancyPolicy
        {
            IsDedicatedGpuAgent = isDedicatedGpuAgent,
            GpuDeviceIds = gpuDeviceIds ?? [],
        });

        return new PodmanContainerExecutionService(
            dockerClient,
            new ImageProvenanceVerifier(Options.Create(new RegistryAllowlist())),
            policy,
            gate ?? new UnimplementedGpuCapabilityGate(),
            new NoOpGpuStateSanitizer(),
            new PassthroughWarmContainerPool(),
            NullLogger<PodmanContainerExecutionService>.Instance);
    }

    private static ContainerExecutionRequest GpuRequest() => new(
        ImageDigest: "mcr.microsoft.com/dotnet/aspnet@sha256:6391fb08009d28f9a74df93ab08711082041d4c79672a4354fbe605ddb817fa1",
        Command: ["/bin/sh", "-c", "echo should-never-run"],
        WorkingDirectoryHostPath: Path.GetTempPath(),
        ResourceLimits: new ContainerResourceLimits(MemoryBytes: 256 * 1024 * 1024, CpuCores: 0.5, DiskBytes: 0),
        NetworkPolicy: NetworkPolicy.None,
        RequiresGpu: true,
        Timeout: TimeSpan.FromSeconds(5));

    [Fact]
    public async Task ExecuteAsync_GpuRequestOnNonDedicatedAgent_IsRefused()
    {
        var service = CreateService(isDedicatedGpuAgent: false);

        var result = await service.ExecuteAsync(GpuRequest());

        Assert.Equal(ContainerExecutionOutcome.Failed, result.Outcome);
        Assert.Contains("not configured as a dedicated single-tenant GPU Agent", result.StdErr);
    }

    [Fact]
    public async Task ExecuteAsync_DedicatedGpuAgentWithNoDeviceIds_IsRefused()
    {
        var service = CreateService(isDedicatedGpuAgent: true, gpuDeviceIds: []);

        var result = await service.ExecuteAsync(GpuRequest());

        Assert.Equal(ContainerExecutionOutcome.Failed, result.Outcome);
        Assert.Contains("not configured as a dedicated single-tenant GPU Agent", result.StdErr);
    }

    [Fact]
    public async Task ExecuteAsync_DedicatedGpuAgentWithUnimplementedCapabilityGate_IsRefused()
    {
        // The default gate wired in production DI (UnimplementedGpuCapabilityGate)
        // must deny, not silently approve, until Phase 5's real R-MOD8 mediator
        // replaces it -- proves there's no accidental default-allow.
        var service = CreateService(isDedicatedGpuAgent: true, gpuDeviceIds: ["0"]);

        var result = await service.ExecuteAsync(GpuRequest());

        Assert.Equal(ContainerExecutionOutcome.Failed, result.Outcome);
        Assert.Contains("R-MOD8", result.StdErr);
    }

    [Fact]
    public async Task ExecuteAsync_CapabilityGateDenies_IsRefusedWithGateReason()
    {
        var denyingGate = new StubGpuCapabilityGate(GpuCapabilityDecision.Denied("Project has not approved GPU capability for this Persona."));
        var service = CreateService(isDedicatedGpuAgent: true, gpuDeviceIds: ["0"], gate: denyingGate);

        var result = await service.ExecuteAsync(GpuRequest());

        Assert.Equal(ContainerExecutionOutcome.Failed, result.Outcome);
        Assert.Contains("has not approved GPU capability", result.StdErr);
    }

    private sealed class NoOpGpuStateSanitizer : IGpuStateSanitizer
    {
        public Task SanitizeAsync(IReadOnlyList<string> gpuDeviceIds, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubGpuCapabilityGate(GpuCapabilityDecision decision) : IGpuCapabilityGate
    {
        public Task<GpuCapabilityDecision> AuthorizeAsync(ContainerExecutionRequest request, CancellationToken ct = default) =>
            Task.FromResult(decision);
    }
}
