namespace Telechron.Sdk.Containers;

public sealed record PooledContainer(string ContainerId, bool WasReused);

// R-SYS10: "Agents maintain warm pools of pre-provisioned containers keyed
// by (Toolchain, project dependency fingerprint)... Deterministic non-LLM
// fixes (R-FIX5) may reuse a pre-warmed isolated container across a bounded
// batch." This interface is deliberately narrow: it only ever hands back a
// container id to *start*, never decides isolation policy -- resource
// limits, network mode, and GPU gating in PodmanContainerExecutionService
// apply identically whether the container came from the pool or was
// created fresh, so pooling cannot become a way to weaken R-SYS6/R-SYS7.
public interface IWarmContainerPool
{
    // Returns a stopped container ready to start for (key, workingDirectoryHostPath),
    // reusing an idle pooled container if one matches exactly, otherwise creating
    // one via createContainer. Never returns a container that is already running.
    Task<PooledContainer> CheckoutAsync(
        WarmPoolKey key,
        string workingDirectoryHostPath,
        Func<CancellationToken, Task<string>> createContainer,
        CancellationToken ct = default);

    // Returns a container to the pool (stopped, not removed) for potential reuse,
    // subject to bounded pool size and TTL -- or removes it outright if the pool
    // is full, the key isn't reusable, or the container is no longer healthy.
    Task ReturnAsync(WarmPoolKey key, string workingDirectoryHostPath, string containerId, CancellationToken ct = default);

    // Pre-pulls the image for a key so the first real execution doesn't pay
    // pull latency. Safe to call redundantly -- pulling an already-present
    // digest is a no-op at the registry/runtime layer.
    Task WarmImageAsync(WarmPoolKey key, CancellationToken ct = default);
}
