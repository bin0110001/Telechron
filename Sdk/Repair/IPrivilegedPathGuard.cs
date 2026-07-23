namespace Telechron.Sdk.Repair;

public sealed record PrivilegedPathCheckResult(bool IsPrivileged, IReadOnlyList<string> MatchedPaths);

// R-SEC4: patches touching Persona definitions, permission/capability-
// evaluation code, the repair pipeline itself, secret-handling code,
// approval-gate logic, module trust policy, or a Project's Design
// Document MUST route to RequireApproval regardless of Project Repair
// Policy. Path-pattern based rather than semantic -- R-FIX7 allows atomic
// multi-file patches, so this must catch a privileged file bundled
// alongside an innocuous one, not just a patch that's "mostly" privileged.
public interface IPrivilegedPathGuard
{
    PrivilegedPathCheckResult Check(PatchDiff patch);
}
