namespace Telechron.Sdk.Containers;

public sealed record GpuCapabilityDecision(bool IsApproved, string Reason)
{
    public static GpuCapabilityDecision Approved(string reason) => new(true, reason);
    public static GpuCapabilityDecision Denied(string reason) => new(false, reason);
}

// R-SYS8: "GPU access requires the same capability-approval path as
// R-MOD8." R-MOD8 (module capability permissions, Project approval,
// non-bypassable Host-side mediation) doesn't exist until Phase 5 — this
// interface is the extension point Phase 5's real capability mediator
// plugs into. Until then, ContainerExecutionService MUST still call
// through this gate rather than assuming approval, so wiring in the real
// R-MOD8 mediator later requires no change at the call site.
public interface IGpuCapabilityGate
{
    Task<GpuCapabilityDecision> AuthorizeAsync(ContainerExecutionRequest request, CancellationToken ct = default);
}
