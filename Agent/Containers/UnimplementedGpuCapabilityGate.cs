using Telechron.Sdk.Containers;

namespace Telechron.Agent.Containers;

// R-SYS8: "GPU access requires the same capability-approval path as
// R-MOD8." The real R-MOD8 capability-permission mediator doesn't exist
// until Phase 5 (Module Runtime & Provider Modules). Until it's wired in,
// every GPU request is denied -- there is no default-approve fallback,
// since that would silently defeat the requirement it's standing in for.
public sealed class UnimplementedGpuCapabilityGate : IGpuCapabilityGate
{
    public Task<GpuCapabilityDecision> AuthorizeAsync(ContainerExecutionRequest request, CancellationToken ct = default) =>
        Task.FromResult(GpuCapabilityDecision.Denied(
            "GPU capability approval (R-MOD8) is not yet implemented (lands in Phase 5) -- GPU requests are refused, not default-approved."));
}
