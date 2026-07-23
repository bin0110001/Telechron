namespace Telechron.Sdk.Repair;

public sealed record DiffScopeCheckResult(bool ExceedsScope, string? Reason);

// R-FIX12: patches exceeding a configurable file-count/line-count
// threshold, or touching files outside the Finding's declared origin
// location, require elevated review regardless of Project Repair Policy.
public interface IRepairDiffScopeGuard
{
    DiffScopeCheckResult Check(PatchDiff patch, IReadOnlyList<string> declaredOriginPaths);
}
