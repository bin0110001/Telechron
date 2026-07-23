namespace Telechron.Sdk.Scheduling;

using Telechron.Sdk.Domain;

public sealed record ResourceLockHandle(Guid ResourceId, string ExclusiveGroup, Guid LockId);

public interface IResourceManager
{
    Task<ResourceLockHandle?> TryAcquireExclusiveLockAsync(Resource resource, TimeSpan timeout, CancellationToken ct = default);
    Task ReleaseLockAsync(ResourceLockHandle handle, CancellationToken ct = default);
    Task<bool> IsResourceLockedAsync(Guid resourceId, CancellationToken ct = default);
}
