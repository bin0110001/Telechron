namespace Telechron.Sdk.Containers;

// R-SYS7: CPU/memory/disk quotas enforced on every untrusted/synthesized/
// module-code container. No default here is "unlimited" — a caller must
// supply real values, since the whole point is that nothing runs unbounded.
public sealed record ContainerResourceLimits(
    long MemoryBytes,
    double CpuCores,
    long DiskBytes);
