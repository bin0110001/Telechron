using System.Collections.Concurrent;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telechron.Sdk.Containers;

namespace Telechron.Agent.Containers;

// R-SYS10: two independent speedups, neither weakening R-SYS6/R-SYS7:
//
// 1. Layer caching (WarmImageAsync): pre-pulls a Toolchain image so the
//    first real execution against it doesn't pay pull latency. Podman's
//    own content-addressed layer store makes a repeat pull of an
//    already-present digest a no-op -- we're not reinventing layer
//    caching, just triggering the pull ahead of need.
// 2. Container reuse (Checkout/Return): a container can only be reused if
//    both the (Toolchain, dependency fingerprint) key AND the exact host
//    working-directory bind path match a prior run -- Docker/Podman binds
//    are fixed at container-create time, so reuse across different
//    workspaces is not possible via this API, and we don't attempt it.
//    Every checkout still goes through the identical create/limits path
//    when no reusable container is idle; pooling only ever saves the
//    create+pull round trip, never the per-run resource/network isolation
//    setup in PodmanContainerExecutionService.
public sealed class WarmContainerPool(
    IDockerClient dockerClient,
    IOptions<WarmContainerPoolOptions> options,
    ILogger<WarmContainerPool> logger) : IWarmContainerPool
{
    private sealed record PoolEntry(string ContainerId, string WorkingDirectoryHostPath, DateTimeOffset IdleSinceUtc);

    // Keyed by WarmPoolKey; each slot holds up to MaxIdleContainersPerKey
    // idle containers. A plain lock per operation is fine at Agent scale
    // (single Agent process, a handful of concurrent Runs at most).
    private readonly ConcurrentDictionary<WarmPoolKey, List<PoolEntry>> _idleByKey = new();
    private readonly object _gate = new();

    public async Task<PooledContainer> CheckoutAsync(
        WarmPoolKey key, string workingDirectoryHostPath, Func<CancellationToken, Task<string>> createContainer, CancellationToken ct = default)
    {
        var reusable = TakeReusable(key, workingDirectoryHostPath);
        if (reusable is not null)
        {
            logger.LogInformation("Reusing warm container {ContainerId} for {ImageDigest}.", reusable, key.ImageDigest);
            return new PooledContainer(reusable, WasReused: true);
        }

        var containerId = await createContainer(ct);
        return new PooledContainer(containerId, WasReused: false);
    }

    public async Task ReturnAsync(WarmPoolKey key, string workingDirectoryHostPath, string containerId, CancellationToken ct = default)
    {
        EvictExpired(key);

        var accepted = false;
        lock (_gate)
        {
            var entries = _idleByKey.GetOrAdd(key, static _ => []);
            if (entries.Count < options.Value.MaxIdleContainersPerKey)
            {
                entries.Add(new PoolEntry(containerId, workingDirectoryHostPath, DateTimeOffset.UtcNow));
                accepted = true;
            }
        }

        if (!accepted)
        {
            await TryRemoveAsync(containerId, ct);
        }
    }

    public async Task WarmImageAsync(WarmPoolKey key, CancellationToken ct = default)
    {
        try
        {
            await dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = key.ImageDigest },
                authConfig: null,
                progress: new Progress<JSONMessage>(),
                ct);
        }
        catch (Exception ex)
        {
            // Pre-warming is a pure optimization -- a failed pre-pull just
            // means the first real execution pays the pull cost itself.
            logger.LogWarning(ex, "Failed to pre-warm image {ImageDigest}; falling back to on-demand pull.", key.ImageDigest);
        }
    }

    private string? TakeReusable(WarmPoolKey key, string workingDirectoryHostPath)
    {
        EvictExpired(key);

        lock (_gate)
        {
            if (!_idleByKey.TryGetValue(key, out var entries))
                return null;

            var index = entries.FindIndex(e => e.WorkingDirectoryHostPath == workingDirectoryHostPath);
            if (index < 0)
                return null;

            var entry = entries[index];
            entries.RemoveAt(index);
            return entry.ContainerId;
        }
    }

    private void EvictExpired(WarmPoolKey key)
    {
        List<PoolEntry> expired = [];
        lock (_gate)
        {
            if (!_idleByKey.TryGetValue(key, out var entries))
                return;

            var cutoff = DateTimeOffset.UtcNow - options.Value.IdleTtl;
            expired = entries.Where(e => e.IdleSinceUtc < cutoff).ToList();
            foreach (var entry in expired)
                entries.Remove(entry);
        }

        foreach (var entry in expired)
        {
            logger.LogInformation("Evicting expired warm container {ContainerId} (idle TTL exceeded).", entry.ContainerId);
            _ = TryRemoveAsync(entry.ContainerId, CancellationToken.None);
        }
    }

    private async Task TryRemoveAsync(string containerId, CancellationToken ct)
    {
        try
        {
            await dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove pooled container {ContainerId}.", containerId);
        }
    }
}
