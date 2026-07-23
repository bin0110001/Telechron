namespace Telechron.Host.Scheduling.Tests;

using Telechron.Host.Scheduling;
using Telechron.Sdk.Domain;

public sealed class ResourceManagerTests
{
    [Fact]
    public async Task TryAcquireExclusiveLockAsync_SameGroup_MutuallyExcludes()
    {
        var manager = new ResourceManager();

        var machineId = Guid.NewGuid();
        var resource1 = new Resource
        {
            Id = Guid.NewGuid(),
            MachineId = machineId,
            Kind = "GPU",
            Name = "NVIDIA RTX 4090",
            ExclusiveGroup = "gpu-group-1"
        };

        var resource2 = new Resource
        {
            Id = Guid.NewGuid(),
            MachineId = machineId,
            Kind = "GPU",
            Name = "NVIDIA RTX 3090",
            ExclusiveGroup = "gpu-group-1"
        };

        var lock1 = await manager.TryAcquireExclusiveLockAsync(resource1, TimeSpan.FromSeconds(1));
        Assert.NotNull(lock1);

        // Attempting to lock second resource in same exclusive group fails
        var lock2 = await manager.TryAcquireExclusiveLockAsync(resource2, TimeSpan.FromSeconds(1));
        Assert.Null(lock2);

        // Releasing lock1 allows lock2 to be acquired
        await manager.ReleaseLockAsync(lock1!);

        var lock2Retry = await manager.TryAcquireExclusiveLockAsync(resource2, TimeSpan.FromSeconds(1));
        Assert.NotNull(lock2Retry);
    }
}
