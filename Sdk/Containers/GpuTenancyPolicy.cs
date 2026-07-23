namespace Telechron.Sdk.Containers;

// R-SYS8: an Agent is either a dedicated, single-tenant GPU Agent (no
// co-scheduled trusted workloads — the whole machine is reserved for
// GPU-requesting containers, one at a time) or it is not GPU-capable for
// untrusted/synthesized execution at all. There is no "shared GPU" option
// for untrusted code: GPU passthrough weakens the container isolation
// boundary, so the mitigation is single-tenancy, not fine-grained sharing.
//
// A settable-property class (not an immutable record) because it's bound
// via IOptions<GpuTenancyPolicy> / services.Configure, matching the same
// pattern as PodmanConnectionOptions and RegistryAllowlist.
public sealed class GpuTenancyPolicy
{
    public bool IsDedicatedGpuAgent { get; set; }
    public IReadOnlyList<string> GpuDeviceIds { get; set; } = [];
}
