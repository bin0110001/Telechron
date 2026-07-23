using Docker.DotNet;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telechron.Agent.Containers;
using Telechron.Sdk.Containers;

namespace Telechron.Agent.Containers.Tests;

// Live-fire tests against a real local Podman machine (R-SYS6/R-SYS7/R-SYS9).
// These prove the execution boundary actually creates, resource-limits,
// network-isolates, and tears down containers -- not just that the code
// compiles against Docker.DotNet's types. Requires a running
// `podman-machine-default` reachable at the default named-pipe endpoint;
// skips (rather than fails) if Podman isn't available in the environment.
public class PodmanContainerExecutionServiceLiveTests : IAsyncLifetime
{
    private const string AspnetImageDigest =
        "mcr.microsoft.com/dotnet/aspnet@sha256:6391fb08009d28f9a74df93ab08711082041d4c79672a4354fbe605ddb817fa1";

    private IDockerClient _dockerClient = null!;
    private bool _podmanAvailable;
    private string _workspaceDir = null!;

    public async Task InitializeAsync()
    {
        _dockerClient = new DockerClientConfiguration(new Uri("npipe://./pipe/podman-machine-default")).CreateClient();
        _workspaceDir = Path.Combine(Path.GetTempPath(), "telechron-container-livetest-" + Guid.NewGuid());
        Directory.CreateDirectory(_workspaceDir);

        try
        {
            await _dockerClient.System.PingAsync();
            _podmanAvailable = true;
        }
        catch
        {
            _podmanAvailable = false;
        }
    }

    public Task DisposeAsync()
    {
        _dockerClient.Dispose();
        if (Directory.Exists(_workspaceDir))
            Directory.Delete(_workspaceDir, recursive: true);
        return Task.CompletedTask;
    }

    private PodmanContainerExecutionService CreateService() =>
        new(_dockerClient, new ImageProvenanceVerifier(Options.Create(new RegistryAllowlist())), NullLogger<PodmanContainerExecutionService>.Instance);

    [SkippableFact]
    public async Task ExecuteAsync_ValidImageAndCommand_RunsAndCapturesStdOut()
    {
        Skip.IfNot(_podmanAvailable, "Podman machine not reachable at npipe://./pipe/podman-machine-default");

        var service = CreateService();
        var request = new ContainerExecutionRequest(
            ImageDigest: AspnetImageDigest,
            Command: ["/bin/sh", "-c", "echo telechron-live-test-marker"],
            WorkingDirectoryHostPath: _workspaceDir,
            ResourceLimits: new ContainerResourceLimits(MemoryBytes: 256 * 1024 * 1024, CpuCores: 0.5, DiskBytes: 0),
            NetworkPolicy: NetworkPolicy.None,
            RequiresGpu: false,
            Timeout: TimeSpan.FromSeconds(30));

        var result = await service.ExecuteAsync(request);

        Assert.Equal(ContainerExecutionOutcome.Completed, result.Outcome);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("telechron-live-test-marker", result.StdOut);
    }

    [SkippableFact]
    public async Task ExecuteAsync_NetworkPolicyNone_ContainerCannotReachExternalHost()
    {
        Skip.IfNot(_podmanAvailable, "Podman machine not reachable at npipe://./pipe/podman-machine-default");

        var service = CreateService();
        var request = new ContainerExecutionRequest(
            ImageDigest: AspnetImageDigest,
            // No network stack at all under NetworkMode "none" -- even loopback-only
            // resolution of an external host must fail; getent exits non-zero.
            Command: ["/bin/sh", "-c", "getent hosts mcr.microsoft.com || echo NETWORK_UNREACHABLE"],
            WorkingDirectoryHostPath: _workspaceDir,
            ResourceLimits: new ContainerResourceLimits(MemoryBytes: 256 * 1024 * 1024, CpuCores: 0.5, DiskBytes: 0),
            NetworkPolicy: NetworkPolicy.None,
            RequiresGpu: false,
            Timeout: TimeSpan.FromSeconds(30));

        var result = await service.ExecuteAsync(request);

        Assert.Equal(ContainerExecutionOutcome.Completed, result.Outcome);
        Assert.Contains("NETWORK_UNREACHABLE", result.StdOut);
    }

    [SkippableFact]
    public async Task ExecuteAsync_MemoryLimitExceeded_ReportsResourceLimitExceeded()
    {
        Skip.IfNot(_podmanAvailable, "Podman machine not reachable at npipe://./pipe/podman-machine-default");

        var service = CreateService();
        var request = new ContainerExecutionRequest(
            ImageDigest: AspnetImageDigest,
            // Repeated string-doubling in a single awk process reliably
            // forces >32MB of resident heap in one process (2^26 bytes at
            // full growth) -- unlike a `cat | head | tail` pipeline where
            // each stage streams through without ever allocating enough to
            // trip the cgroup limit.
            Command: ["/bin/sh", "-c", "awk 'BEGIN{s=\"x\"; for(i=0;i<26;i++) s=s s; print length(s)}'"],
            WorkingDirectoryHostPath: _workspaceDir,
            ResourceLimits: new ContainerResourceLimits(MemoryBytes: 32 * 1024 * 1024, CpuCores: 0.5, DiskBytes: 0),
            NetworkPolicy: NetworkPolicy.None,
            RequiresGpu: false,
            Timeout: TimeSpan.FromSeconds(30));

        var result = await service.ExecuteAsync(request);

        Assert.Equal(ContainerExecutionOutcome.ResourceLimitExceeded, result.Outcome);
    }

    [SkippableFact]
    public async Task ExecuteAsync_CommandExceedsTimeout_ReportsTimedOut()
    {
        Skip.IfNot(_podmanAvailable, "Podman machine not reachable at npipe://./pipe/podman-machine-default");

        var service = CreateService();
        var request = new ContainerExecutionRequest(
            ImageDigest: AspnetImageDigest,
            Command: ["/bin/sh", "-c", "sleep 30"],
            WorkingDirectoryHostPath: _workspaceDir,
            ResourceLimits: new ContainerResourceLimits(MemoryBytes: 256 * 1024 * 1024, CpuCores: 0.5, DiskBytes: 0),
            NetworkPolicy: NetworkPolicy.None,
            RequiresGpu: false,
            Timeout: TimeSpan.FromSeconds(3));

        var result = await service.ExecuteAsync(request);

        Assert.Equal(ContainerExecutionOutcome.TimedOut, result.Outcome);
    }

    [SkippableFact]
    public async Task ExecuteAsync_NonDigestImageReference_RefusesWithoutContactingPodman()
    {
        Skip.IfNot(_podmanAvailable, "Podman machine not reachable at npipe://./pipe/podman-machine-default");

        var service = CreateService();
        var request = new ContainerExecutionRequest(
            ImageDigest: "mcr.microsoft.com/dotnet/aspnet:8.0",
            Command: ["/bin/sh", "-c", "echo should-never-run"],
            WorkingDirectoryHostPath: _workspaceDir,
            ResourceLimits: new ContainerResourceLimits(MemoryBytes: 256 * 1024 * 1024, CpuCores: 0.5, DiskBytes: 0),
            NetworkPolicy: NetworkPolicy.None,
            RequiresGpu: false,
            Timeout: TimeSpan.FromSeconds(30));

        var result = await service.ExecuteAsync(request);

        Assert.Equal(ContainerExecutionOutcome.Failed, result.Outcome);
        Assert.Contains("not a digest-pinned reference", result.StdErr);
    }

    [SkippableFact]
    public async Task ExecuteAsync_AfterCompletion_ContainerIsRemoved()
    {
        Skip.IfNot(_podmanAvailable, "Podman machine not reachable at npipe://./pipe/podman-machine-default");

        var service = CreateService();
        var request = new ContainerExecutionRequest(
            ImageDigest: AspnetImageDigest,
            Command: ["/bin/sh", "-c", "echo cleanup-check"],
            WorkingDirectoryHostPath: _workspaceDir,
            ResourceLimits: new ContainerResourceLimits(MemoryBytes: 256 * 1024 * 1024, CpuCores: 0.5, DiskBytes: 0),
            NetworkPolicy: NetworkPolicy.None,
            RequiresGpu: false,
            Timeout: TimeSpan.FromSeconds(30));

        await service.ExecuteAsync(request);

        var containers = await _dockerClient.Containers.ListContainersAsync(new Docker.DotNet.Models.ContainersListParameters { All = true });
        Assert.DoesNotContain(containers, c => c.Command.Contains("cleanup-check"));
    }
}
