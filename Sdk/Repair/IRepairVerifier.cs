using Telechron.Sdk.Modules.Runners;

namespace Telechron.Sdk.Repair;

public sealed record VerifyResult(bool Succeeded, TestRunResult? TestRunResult, string RawOutput);

// R-FIX2 "Verify (Build + Self-Test)" -- always runs inside the Phase 4
// container boundary (R-SYS6: workload execution never happens in-process
// on the Host), using the Project's own Toolchain/TestRunner module
// descriptors so Verify exercises the exact build/test command a real
// contributor would run, not a hardcoded one. One seam, reused by every
// Finding origin (R-FIX4 routes WHAT gets fixed, not HOW it's verified).
public interface IRepairVerifier
{
    Task<VerifyResult> VerifyAsync(string projectRootPath, CancellationToken ct = default);
}
