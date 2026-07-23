namespace Telechron.Sdk.Repair;

// R-FIX2/R-DM3a: "SnapshotRef" is opaque outside the repair-VCS layer --
// callers pass it around as a handle (a git commit SHA under the actual
// LibGit2Sharp-backed implementation) without needing to know it's git.
public sealed record SnapshotRef(string Value)
{
    public override string ToString() => Value;
}
