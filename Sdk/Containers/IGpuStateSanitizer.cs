namespace Telechron.Sdk.Containers;

// R-SYS8: "GPU memory is cleared between tenants." Runs after every
// GPU-requesting container is torn down, before the device is handed to
// the next request, so no tenant's GPU memory (model weights, tensors,
// leftover allocations) is readable by the next.
public interface IGpuStateSanitizer
{
    Task SanitizeAsync(IReadOnlyList<string> gpuDeviceIds, CancellationToken ct = default);
}
