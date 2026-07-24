namespace Telechron.Sdk.Repair;

// R-ENG4 (single pipeline): RepairPipelineOrchestrator's own construction
// needs a Project-specific Toolchain/TestRunner (for its Verify stage) and
// LlmConnection (for Generate Fix / drift detection) -- none of which are
// fixed/global, they vary per Project being repaired. This factory is the
// one place that resolves "which real module/connection applies to this
// Project" and builds an orchestrator wired to them, so every caller
// (Host Sentinel's self-repair, and any future repair-triggering surface)
// gets the real thing rather than re-implementing this resolution inline.
public interface IRepairPipelineFactory
{
    // machineId: the Agent-owned Machine the Verify stage's container run
    // dispatches to. Caller-supplied rather than auto-selected -- same
    // convention as StorefrontAcquisitionContext.TargetMachineId, since
    // no Project-to-Machine assignment policy exists anywhere yet.
    Task<RepairPipelineOrchestrator> CreateForProjectAsync(Guid projectId, Guid machineId, CancellationToken ct = default);
}
