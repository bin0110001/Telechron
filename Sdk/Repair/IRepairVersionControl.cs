namespace Telechron.Sdk.Repair;

public sealed record ApplyPatchResult(bool Succeeded, string? ErrorMessage);
public sealed record CommitResult(string CommitReference);

// R-NS2/R-FIX2/R-FIX7: the one snapshot/patch/commit/revert mechanism
// every repair path uses -- no bespoke fix/verify/revert path anywhere
// (R-ENG4) means no capability gets its own git-adjacent logic, they all
// go through this. Backed by real git (LibGit2Sharp) under
// Host/Repair/GitRepairVersionControl.cs; the interface itself has no git
// vocabulary so a non-git-repo Project (if that's ever supported) could
// swap implementations without callers changing.
public interface IRepairVersionControl
{
    // R-FIX2 "Snapshot": captures the exact state of the working tree
    // before a repair attempt touches anything, so Revert can restore it
    // exactly and Verify can diff against a known-good baseline (R-MOD4a's
    // pre-patch falsifiability check reuses this same "run self-test
    // against the pre-patch snapshot" idea, at the repair-pipeline level).
    Task<SnapshotRef> SnapshotAsync(string projectRootPath, CancellationToken ct = default);

    // Computes the diff between the current working tree and a prior
    // snapshot -- used both to capture what Generate Fix produced and (at
    // Verify time) to confirm Apply didn't touch anything outside what
    // was intended.
    Task<PatchDiff> DiffAgainstSnapshotAsync(string projectRootPath, SnapshotRef snapshot, CancellationToken ct = default);

    // R-FIX7: applies every file change in the patch as one atomic
    // operation -- if any file fails to apply, none are applied.
    Task<ApplyPatchResult> ApplyAsync(string projectRootPath, PatchDiff patch, CancellationToken ct = default);

    // Restores the working tree to exactly the given snapshot, discarding
    // any changes made since (R-FIX2's "Revert on Failure").
    Task RevertToSnapshotAsync(string projectRootPath, SnapshotRef snapshot, CancellationToken ct = default);

    // R-FIX2 "Commit": only reached after Verify + (if required) Approval
    // Gate both pass. commitMessage carries the provenance summary (R-SEC3
    // populates the full record separately, stored independently per its
    // own requirement -- this message is human-readable context, not the
    // attestation itself).
    Task<CommitResult> CommitAsync(string projectRootPath, string commitMessage, string authorName, string authorEmail, CancellationToken ct = default);
}
