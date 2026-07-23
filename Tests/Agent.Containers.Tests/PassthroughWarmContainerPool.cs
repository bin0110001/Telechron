using Telechron.Sdk.Containers;

namespace Telechron.Agent.Containers.Tests;

// Test double for tests that don't exercise R-SYS10 pooling behavior
// directly -- always creates fresh via the supplied factory, never reuses.
internal sealed class PassthroughWarmContainerPool : IWarmContainerPool
{
    public async Task<PooledContainer> CheckoutAsync(
        WarmPoolKey key, string workingDirectoryHostPath, Func<CancellationToken, Task<string>> createContainer, CancellationToken ct = default) =>
        new(await createContainer(ct), WasReused: false);

    public Task ReturnAsync(WarmPoolKey key, string workingDirectoryHostPath, string containerId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task WarmImageAsync(WarmPoolKey key, CancellationToken ct = default) => Task.CompletedTask;
}
