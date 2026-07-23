namespace Telechron.Host.Scheduling;

using System.Collections.Concurrent;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Scheduling;

public sealed class ResourceManager : IResourceManager
{
    private readonly ConcurrentDictionary<string, ResourceLockHandle> _locksByGroup = new();

    public Task<ResourceLockHandle?> TryAcquireExclusiveLockAsync(Resource resource, TimeSpan timeout, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(resource.ExclusiveGroup))
        {
            // Null or empty ExclusiveGroup means unconstrained resource
            return Task.FromResult<ResourceLockHandle?>(new ResourceLockHandle(resource.Id, string.Empty, Guid.NewGuid()));
        }

        var groupKey = resource.ExclusiveGroup;
        var handle = new ResourceLockHandle(resource.Id, groupKey, Guid.NewGuid());

        if (_locksByGroup.TryAdd(groupKey, handle))
        {
            return Task.FromResult<ResourceLockHandle?>(handle);
        }

        return Task.FromResult<ResourceLockHandle?>(null);
    }

    public Task ReleaseLockAsync(ResourceLockHandle handle, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(handle.ExclusiveGroup))
        {
            if (_locksByGroup.TryGetValue(handle.ExclusiveGroup, out var current) && current.LockId == handle.LockId)
            {
                _locksByGroup.TryRemove(handle.ExclusiveGroup, out _);
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsResourceLockedAsync(Guid resourceId, CancellationToken ct = default)
    {
        var isLocked = _locksByGroup.Values.Any(h => h.ResourceId == resourceId);
        return Task.FromResult(isLocked);
    }
}
