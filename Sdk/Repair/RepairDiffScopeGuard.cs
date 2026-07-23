namespace Telechron.Sdk.Repair;

public sealed record RepairDiffScopeOptions(int MaxFileCount = 5, int MaxTotalLineCount = 400);

// Default R-FIX12 implementation. Two independent triggers: raw
// size (file-count/line-count threshold) and origin mismatch (touching a
// file the Finding never declared as its origin) -- either alone is
// enough to require elevated review, since a small out-of-origin change
// is exactly the "bundle a privilege escalation with a legitimate fix"
// shape R-SEC4's own note warns about.
public sealed class RepairDiffScopeGuard(RepairDiffScopeOptions options) : IRepairDiffScopeGuard
{
    public RepairDiffScopeGuard() : this(new RepairDiffScopeOptions())
    {
    }

    public DiffScopeCheckResult Check(PatchDiff patch, IReadOnlyList<string> declaredOriginPaths)
    {
        if (patch.FileChanges.Count > options.MaxFileCount)
        {
            return new DiffScopeCheckResult(true,
                $"Patch touches {patch.FileChanges.Count} files, exceeding the {options.MaxFileCount}-file scope limit.");
        }

        var totalLines = patch.FileChanges.Sum(f => CountChangedLines(f.UnifiedDiff));
        if (totalLines > options.MaxTotalLineCount)
        {
            return new DiffScopeCheckResult(true,
                $"Patch changes {totalLines} lines, exceeding the {options.MaxTotalLineCount}-line scope limit.");
        }

        if (declaredOriginPaths.Count > 0)
        {
            var outOfOrigin = patch.TouchedFilePaths
                .Where(p => !declaredOriginPaths.Contains(p, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (outOfOrigin.Count > 0)
            {
                return new DiffScopeCheckResult(true,
                    $"Patch touches files outside the Finding's declared origin: {string.Join(", ", outOfOrigin)}.");
            }
        }

        return new DiffScopeCheckResult(false, null);
    }

    private static int CountChangedLines(string unifiedDiff) =>
        unifiedDiff
            .Split('\n')
            .Count(line => line.Length > 0 && (line[0] == '+' || line[0] == '-') && !line.StartsWith("+++", StringComparison.Ordinal) && !line.StartsWith("---", StringComparison.Ordinal));
}
