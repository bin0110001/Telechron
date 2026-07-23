using System.Diagnostics;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telechron.Agent.Containers;
using Telechron.Sdk.Containers;

namespace Telechron.Agent.Containers.Tests;

// R-SYS10 live proof, against the real local Podman machine: a repeat
// execution against the same (Toolchain, dependency fingerprint) + working
// directory reuses the prior container instead of creating a new one, and
// is measurably faster than the first (cold) execution. Skips rather than
// fails if Podman isn't reachable.
[Trait("Category", "Live")]
public class WarmContainerPoolLiveTests : IAsyncLifetime
{
    private const string AspnetImageDigest =
        "mcr.microsoft.com/dotnet/aspnet@sha256:6391fb08009d28f9a74df93ab08711082041d4c79672a4354fbe605ddb817fa1";

    private IDockerClient _dockerClient = null!;
    private bool _podmanAvailable;
    private string _workspaceDir = null!;

    public async Task InitializeAsync()
    {
        _dockerClient = new DockerClientConfiguration(new Uri("npipe://./pipe/podman-machine-default")).CreateClient();
        _workspaceDir = Path.Combine(Path.GetTempPath(), "telechron-warmpool-livetest-" + Guid.NewGuid());
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

    [SkippableFact]
    public async Task ExecuteAsync_RepeatRequestSameKeyAndWorkspace_ReusesContainerAndIsFaster()
    {
        Skip.IfNot(_podmanAvailable, "Podman machine not reachable at npipe://./pipe/podman-machine-default");

        var pool = new WarmContainerPool(
            _dockerClient, Options.Create(new WarmContainerPoolOptions()), NullLogger<WarmContainerPool>.Instance);
        var service = new PodmanContainerExecutionService(
            _dockerClient,
            new ImageProvenanceVerifier(Options.Create(new RegistryAllowlist())),
            Options.Create(new GpuTenancyPolicy()),
            new UnimplementedGpuCapabilityGate(),
            new NoOpSanitizer(),
            pool,
            NullLogger<PodmanContainerExecutionService>.Instance);

        var poolKey = new WarmPoolKey(AspnetImageDigest, "test-dependency-fingerprint");
        var request = new ContainerExecutionRequest(
            ImageDigest: AspnetImageDigest,
            Command: ["/bin/sh", "-c", "echo warm-pool-marker"],
            WorkingDirectoryHostPath: _workspaceDir,
            ResourceLimits: new ContainerResourceLimits(MemoryBytes: 256 * 1024 * 1024, CpuCores: 0.5, DiskBytes: 0),
            NetworkPolicy: NetworkPolicy.None,
            RequiresGpu: false,
            Timeout: TimeSpan.FromSeconds(30),
            WarmPoolKey: poolKey);

        var beforeFirst = await ListContainerIdsAsync();
        var firstStopwatch = Stopwatch.StartNew();
        var firstResult = await service.ExecuteAsync(request);
        firstStopwatch.Stop();
        Assert.Equal(ContainerExecutionOutcome.Completed, firstResult.Outcome);

        // The completed container must be sitting idle in the pool now,
        // not removed -- prove that directly before even running the
        // second request, so a false pass isn't possible if reuse silently
        // fell back to create-fresh but Podman happened to be fast twice.
        var afterFirst = await ListContainerIdsAsync();
        var pooledContainerId = afterFirst.Except(beforeFirst).SingleOrDefault();
        Assert.False(string.IsNullOrEmpty(pooledContainerId));

        var secondStopwatch = Stopwatch.StartNew();
        var secondResult = await service.ExecuteAsync(request);
        secondStopwatch.Stop();
        Assert.Equal(ContainerExecutionOutcome.Completed, secondResult.Outcome);

        var afterSecond = await ListContainerIdsAsync();
        Assert.Contains(pooledContainerId, afterSecond);
        // No new container id appeared -- the second run reused the exact
        // same container object rather than creating a fresh one.
        Assert.Equal(afterFirst.Count, afterSecond.Count);

        Assert.True(secondStopwatch.Elapsed < firstStopwatch.Elapsed,
            $"Expected reuse to be faster: first={firstStopwatch.Elapsed}, second={secondStopwatch.Elapsed}");

        await _dockerClient.Containers.RemoveContainerAsync(pooledContainerId!, new ContainerRemoveParameters { Force = true });
    }

    [SkippableFact]
    public async Task ExecuteAsync_SameKeyDifferentWorkspace_DoesNotReuse()
    {
        Skip.IfNot(_podmanAvailable, "Podman machine not reachable at npipe://./pipe/podman-machine-default");

        var otherWorkspaceDir = Path.Combine(Path.GetTempPath(), "telechron-warmpool-livetest-other-" + Guid.NewGuid());
        Directory.CreateDirectory(otherWorkspaceDir);
        try
        {
            var pool = new WarmContainerPool(
                _dockerClient, Options.Create(new WarmContainerPoolOptions()), NullLogger<WarmContainerPool>.Instance);
            var service = new PodmanContainerExecutionService(
                _dockerClient,
                new ImageProvenanceVerifier(Options.Create(new RegistryAllowlist())),
                Options.Create(new GpuTenancyPolicy()),
                new UnimplementedGpuCapabilityGate(),
                new NoOpSanitizer(),
                pool,
                NullLogger<PodmanContainerExecutionService>.Instance);

            var poolKey = new WarmPoolKey(AspnetImageDigest, "test-dependency-fingerprint-2");
            ContainerExecutionRequest RequestFor(string workspace) => new(
                ImageDigest: AspnetImageDigest,
                Command: ["/bin/sh", "-c", "echo different-workspace-marker"],
                WorkingDirectoryHostPath: workspace,
                ResourceLimits: new ContainerResourceLimits(MemoryBytes: 256 * 1024 * 1024, CpuCores: 0.5, DiskBytes: 0),
                NetworkPolicy: NetworkPolicy.None,
                RequiresGpu: false,
                Timeout: TimeSpan.FromSeconds(30),
                WarmPoolKey: poolKey);

            var beforeFirst = await ListContainerIdsAsync();
            await service.ExecuteAsync(RequestFor(_workspaceDir));
            var afterFirst = await ListContainerIdsAsync();
            var firstContainerId = afterFirst.Except(beforeFirst).Single();

            await service.ExecuteAsync(RequestFor(otherWorkspaceDir));
            var afterSecond = await ListContainerIdsAsync();

            // Different bind mount -> not reusable -> a second distinct
            // container must exist alongside the first (both idle-pooled).
            Assert.Equal(2, afterSecond.Except(beforeFirst).Count());
            Assert.Contains(firstContainerId, afterSecond);

            foreach (var id in afterSecond.Except(beforeFirst))
                await _dockerClient.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = true });
        }
        finally
        {
            Directory.Delete(otherWorkspaceDir, recursive: true);
        }
    }

    // Diffs the full container list rather than filtering server-side --
    // [assembly: CollectionBehavior(DisableTestParallelization = true)]
    // (AssemblyInfo.cs) keeps this assembly's own tests from interleaving;
    // any remaining risk is from unrelated containers on this dev machine
    // being created/removed in the same instant, which is what the
    // Except()-based diffs below are already robust to.
    private async Task<List<string>> ListContainerIdsAsync()
    {
        var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters { All = true });
        return containers.Select(c => c.ID).ToList();
    }

    private sealed class NoOpSanitizer : IGpuStateSanitizer
    {
        public Task SanitizeAsync(IReadOnlyList<string> gpuDeviceIds, CancellationToken ct = default) => Task.CompletedTask;
    }
}
