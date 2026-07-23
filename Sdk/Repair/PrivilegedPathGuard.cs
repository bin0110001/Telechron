namespace Telechron.Sdk.Repair;

// Default R-SEC4 implementation. Segment-based matching against the
// repair-relevant subsystem directories/namespaces of THIS repo's own
// layout -- deliberately conservative (a path is flagged if any privileged
// segment appears anywhere in it) since a false positive costs one extra
// human review, while a false negative lets a privilege escalation
// auto-commit.
public sealed class PrivilegedPathGuard : IPrivilegedPathGuard
{
    private static readonly string[] PrivilegedSegments =
    [
        "Persona",
        "Permissions",
        "PermissionMediator",
        "Repair",
        "Secret",
        "Approval",
        "Trust",
        "ModuleTrustEvaluator",
        "DesignDocument",
        "Requirement",
    ];

    public PrivilegedPathCheckResult Check(PatchDiff patch)
    {
        var matched = patch.TouchedFilePaths
            .Where(IsPrivilegedPath)
            .ToList();

        return new PrivilegedPathCheckResult(matched.Count > 0, matched);
    }

    private static bool IsPrivilegedPath(string relativePath) =>
        PrivilegedSegments.Any(segment =>
            relativePath.Contains(segment, StringComparison.OrdinalIgnoreCase));
}
