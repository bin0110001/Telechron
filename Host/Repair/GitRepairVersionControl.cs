using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Telechron.Sdk.Repair;

namespace Telechron.Host.Repair;

// R-NS2/R-FIX2/R-FIX7: git-backed implementation. All repair activity
// happens on a dedicated branch (RepairBranchName) that the pipeline
// creates/resets from the current HEAD -- the user's actual checked-out
// branch is never touched by Snapshot/Apply/Verify/Revert. Only a
// successful, approved Commit ever produces something meant to reach the
// user's real branch, and even then this class only commits to the
// repair branch itself; merging/fast-forwarding the user's branch is a
// separate, explicit, privileged operation this class does not perform.
public sealed class GitRepairVersionControl(ILogger<GitRepairVersionControl> logger) : IRepairVersionControl
{
    public const string RepairBranchName = "telechron/repair-work";

    public Task<SnapshotRef> SnapshotAsync(string projectRootPath, CancellationToken ct = default)
    {
        using var repo = OpenOrInitRepository(projectRootPath);
        var signature = new Signature("Telechron Repair Pipeline", "repair@telechron.internal", DateTimeOffset.UtcNow);

        // A freshly `git init`'d repo has no commits and no real branch
        // yet (HEAD is an unborn branch, "master"/"main" doesn't exist
        // until the first commit) -- CreateBranch(name) internally
        // resolves "master" as its commit-ish and throws NotFoundException
        // in that state. Committing on the current (unborn) HEAD first
        // gives every subsequent CreateBranch/Checkout a real commit to
        // branch from, whether this is the very first snapshot ever taken
        // or a later one.
        Commands.Stage(repo, "*");
        if (repo.Head.Tip is null)
        {
            repo.Commit("Repair pipeline snapshot", signature, signature, new CommitOptions { AllowEmptyCommit = true });
        }

        var repairBranch = repo.Branches[RepairBranchName] ?? repo.CreateBranch(RepairBranchName);
        Commands.Checkout(repo, repairBranch);

        // Stage and commit whatever is currently in the working tree onto
        // the repair branch -- this is the restore point. An empty commit
        // (nothing changed since the branch's last state) is allowed:
        // AllowEmptyCommit handles the "snapshot right after a prior
        // revert, nothing new yet" case without throwing.
        Commands.Stage(repo, "*");
        var commit = repo.Commit("Repair pipeline snapshot", signature, signature,
            new CommitOptions { AllowEmptyCommit = true });

        logger.LogInformation("Snapshot taken at {Sha} for {ProjectRoot}.", commit.Sha, projectRootPath);
        return Task.FromResult(new SnapshotRef(commit.Sha));
    }

    public Task<PatchDiff> DiffAgainstSnapshotAsync(string projectRootPath, SnapshotRef snapshot, CancellationToken ct = default)
    {
        using var repo = new Repository(projectRootPath);
        var snapshotCommit = repo.Lookup<Commit>(snapshot.Value)
            ?? throw new InvalidOperationException($"Snapshot commit '{snapshot.Value}' not found.");

        var comparison = repo.Diff.Compare<Patch>(snapshotCommit.Tree, DiffTargets.WorkingDirectory);
        var fileChanges = comparison.Select(entry => new PatchFileChange(entry.Path, entry.Patch)).ToList();

        return Task.FromResult(new PatchDiff(fileChanges));
    }

    public Task<ApplyPatchResult> ApplyAsync(string projectRootPath, PatchDiff patch, CancellationToken ct = default)
    {
        // R-FIX7: atomic -- write every file first, and only if ALL writes
        // succeed do we consider this Applied. A failure partway through
        // leaves the working tree with some files changed; the caller's
        // job (RepairPipeline) is to always follow a failed Apply with
        // RevertToSnapshotAsync, so a partial write never survives.
        var appliedPaths = new List<string>();
        try
        {
            foreach (var change in patch.FileChanges)
            {
                var fullPath = ResolveWithinRoot(projectRootPath, change.RelativePath);
                var newContent = UnifiedDiffApplier.Apply(
                    File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty, change.UnifiedDiff);

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, newContent);
                appliedPaths.Add(fullPath);
            }

            return Task.FromResult(new ApplyPatchResult(true, null));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Patch apply failed for {ProjectRoot} after {AppliedCount} file(s).", projectRootPath, appliedPaths.Count);
            return Task.FromResult(new ApplyPatchResult(false, ex.Message));
        }
    }

    public Task RevertToSnapshotAsync(string projectRootPath, SnapshotRef snapshot, CancellationToken ct = default)
    {
        using var repo = new Repository(projectRootPath);
        var snapshotCommit = repo.Lookup<Commit>(snapshot.Value)
            ?? throw new InvalidOperationException($"Snapshot commit '{snapshot.Value}' not found.");

        repo.Reset(ResetMode.Hard, snapshotCommit);
        logger.LogInformation("Reverted {ProjectRoot} to snapshot {Sha}.", projectRootPath, snapshot.Value);
        return Task.CompletedTask;
    }

    public Task<CommitResult> CommitAsync(
        string projectRootPath, string commitMessage, string authorName, string authorEmail, CancellationToken ct = default)
    {
        using var repo = new Repository(projectRootPath);
        Commands.Stage(repo, "*");

        var signature = new Signature(authorName, authorEmail, DateTimeOffset.UtcNow);
        var commit = repo.Commit(commitMessage, signature, signature);

        logger.LogInformation("Committed {Sha} on {Branch} for {ProjectRoot}.", commit.Sha, repo.Head.FriendlyName, projectRootPath);
        return Task.FromResult(new CommitResult(commit.Sha));
    }

    private static Repository OpenOrInitRepository(string projectRootPath)
    {
        if (!Repository.IsValid(projectRootPath))
            Repository.Init(projectRootPath);
        return new Repository(projectRootPath);
    }

    private static string ResolveWithinRoot(string projectRootPath, string relativePath)
    {
        // Defense in depth against a Generate Fix result naming a path
        // outside the project root -- the patch's own relative paths are
        // LLM-influenced (or deterministic-fix-influenced) content, so
        // never trust them to stay inside the root without checking.
        var fullPath = Path.GetFullPath(Path.Combine(projectRootPath, relativePath));
        var normalizedRoot = Path.GetFullPath(projectRootPath);
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Patch file path '{relativePath}' resolves outside the project root.");
        return fullPath;
    }
}
