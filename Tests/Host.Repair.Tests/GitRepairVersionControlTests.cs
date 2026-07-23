using LibGit2Sharp;
using Microsoft.Extensions.Logging.Abstractions;
using Telechron.Host.Repair;
using Telechron.Sdk.Repair;

namespace Telechron.Host.Repair.Tests;

// Real git operations against a real temp repository (LibGit2Sharp, not
// mocked) -- proves snapshot/apply/diff/revert/commit actually work as a
// real version-control mechanism, since this is entirely greenfield for
// Phase 7 and the whole repair pipeline depends on it being correct.
public sealed class GitRepairVersionControlTests : IDisposable
{
    private readonly string _repoDir;
    private readonly GitRepairVersionControl _vcs = new(NullLogger<GitRepairVersionControl>.Instance);

    public GitRepairVersionControlTests()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), "telechron-repair-vcs-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_repoDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoDir))
        {
            // Git repos leave read-only files under .git/objects on some
            // platforms -- clear attributes before delete so cleanup
            // doesn't fail.
            foreach (var file in Directory.GetFiles(_repoDir, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_repoDir, recursive: true);
        }
    }

    [Fact]
    public async Task SnapshotAsync_NewRepo_InitializesGitAndReturnsCommitSha()
    {
        await File.WriteAllTextAsync(Path.Combine(_repoDir, "hello.txt"), "hello");

        var snapshot = await _vcs.SnapshotAsync(_repoDir);

        Assert.NotEmpty(snapshot.Value);
        Assert.True(Directory.Exists(Path.Combine(_repoDir, ".git")));
    }

    [Fact]
    public async Task SnapshotAsync_CreatesRepairBranchWithoutTouchingOtherBranches()
    {
        await File.WriteAllTextAsync(Path.Combine(_repoDir, "hello.txt"), "hello");
        using (var repo = new LibGit2Sharp.Repository(LibGit2Sharp.Repository.Init(_repoDir)))
        {
            LibGit2Sharp.Commands.Stage(repo, "*");
            var sig = new LibGit2Sharp.Signature("test", "test@test.com", DateTimeOffset.UtcNow);
            repo.Commit("initial", sig, sig, new LibGit2Sharp.CommitOptions());
        }

        await _vcs.SnapshotAsync(_repoDir);

        using var afterRepo = new LibGit2Sharp.Repository(_repoDir);
        Assert.Contains(afterRepo.Branches, b => b.FriendlyName == GitRepairVersionControl.RepairBranchName);
        Assert.Equal(GitRepairVersionControl.RepairBranchName, afterRepo.Head.FriendlyName);
    }

    [Fact]
    public async Task ApplyAsync_SimpleAddition_ProducesExpectedContent()
    {
        var filePath = Path.Combine(_repoDir, "code.txt");
        await File.WriteAllTextAsync(filePath, "line1\nline2\nline3\n");
        var snapshot = await _vcs.SnapshotAsync(_repoDir);

        var diff = """
            --- a/code.txt
            +++ b/code.txt
            @@ -1,3 +1,4 @@
             line1
            +inserted
             line2
             line3
            """;
        var patch = new PatchDiff([new PatchFileChange("code.txt", diff)]);

        var result = await _vcs.ApplyAsync(_repoDir, patch);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var content = await File.ReadAllTextAsync(filePath);
        Assert.Equal("line1\ninserted\nline2\nline3\n", content);
    }

    [Fact]
    public async Task ApplyAsync_RemovalAndAddition_ProducesExpectedContent()
    {
        var filePath = Path.Combine(_repoDir, "code.txt");
        await File.WriteAllTextAsync(filePath, "a\nb\nc\nd\n");
        await _vcs.SnapshotAsync(_repoDir);

        var diff = """
            --- a/code.txt
            +++ b/code.txt
            @@ -1,4 +1,4 @@
             a
            -b
            +B
             c
             d
            """;
        var patch = new PatchDiff([new PatchFileChange("code.txt", diff)]);

        var result = await _vcs.ApplyAsync(_repoDir, patch);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("a\nB\nc\nd\n", await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task ApplyAsync_PathEscapesProjectRoot_IsRejected()
    {
        await _vcs.SnapshotAsync(_repoDir);
        var diff = """
            --- a/x
            +++ b/x
            @@ -0,0 +1,1 @@
            +evil
            """;
        var patch = new PatchDiff([new PatchFileChange("../../../etc/evil.txt", diff)]);

        var result = await _vcs.ApplyAsync(_repoDir, patch);

        Assert.False(result.Succeeded);
        Assert.Contains("outside the project root", result.ErrorMessage);
    }

    [Fact]
    public async Task RevertToSnapshotAsync_DiscardsChangesMadeAfterSnapshot()
    {
        var filePath = Path.Combine(_repoDir, "code.txt");
        await File.WriteAllTextAsync(filePath, "original\n");
        var snapshot = await _vcs.SnapshotAsync(_repoDir);

        await File.WriteAllTextAsync(filePath, "corrupted-by-a-bad-patch\n");

        await _vcs.RevertToSnapshotAsync(_repoDir, snapshot);

        // Reset via git checkout can normalize line endings per the
        // platform's core.autocrlf config -- assert on content, not
        // byte-exact line endings.
        var content = await File.ReadAllTextAsync(filePath);
        Assert.Equal("original", content.TrimEnd());
    }

    [Fact]
    public async Task DiffAgainstSnapshotAsync_AfterApply_ReportsTheChange()
    {
        var filePath = Path.Combine(_repoDir, "code.txt");
        await File.WriteAllTextAsync(filePath, "line1\nline2\n");
        var snapshot = await _vcs.SnapshotAsync(_repoDir);

        var diff = """
            --- a/code.txt
            +++ b/code.txt
            @@ -1,2 +1,2 @@
             line1
            -line2
            +line2-modified
            """;
        await _vcs.ApplyAsync(_repoDir, new PatchDiff([new PatchFileChange("code.txt", diff)]));

        var computedDiff = await _vcs.DiffAgainstSnapshotAsync(_repoDir, snapshot);

        Assert.Single(computedDiff.FileChanges);
        Assert.Equal("code.txt", computedDiff.FileChanges[0].RelativePath);
        Assert.Contains("line2-modified", computedDiff.FileChanges[0].UnifiedDiff);
    }

    [Fact]
    public async Task CommitAsync_ProducesRealCommitWithCorrectAuthorAndContent()
    {
        var filePath = Path.Combine(_repoDir, "code.txt");
        await File.WriteAllTextAsync(filePath, "v1\n");
        await _vcs.SnapshotAsync(_repoDir);

        await File.WriteAllTextAsync(filePath, "v2\n");
        var commitResult = await _vcs.CommitAsync(_repoDir, "Fix: update code.txt", "Telechron Repair", "repair@telechron.internal");

        Assert.NotEmpty(commitResult.CommitReference);

        using var repo = new LibGit2Sharp.Repository(_repoDir);
        var commit = repo.Lookup<LibGit2Sharp.Commit>(commitResult.CommitReference);
        Assert.NotNull(commit);
        Assert.Equal("Fix: update code.txt\n", commit!.Message);
        Assert.Equal("Telechron Repair", commit.Author.Name);
        Assert.Equal("repair@telechron.internal", commit.Author.Email);
    }

    [Fact]
    public async Task FullLifecycle_SnapshotApplyVerifyCommit_EndsWithCorrectFinalState()
    {
        var filePath = Path.Combine(_repoDir, "buggy.cs");
        await File.WriteAllTextAsync(filePath, "int Add(int a, int b) => a - b; // BUG\n");
        var snapshot = await _vcs.SnapshotAsync(_repoDir);

        var diff = """
            --- a/buggy.cs
            +++ b/buggy.cs
            @@ -1 +1 @@
            -int Add(int a, int b) => a - b; // BUG
            +int Add(int a, int b) => a + b; // fixed
            """;
        var applyResult = await _vcs.ApplyAsync(_repoDir, new PatchDiff([new PatchFileChange("buggy.cs", diff)]));
        Assert.True(applyResult.Succeeded, applyResult.ErrorMessage);

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("a + b", content);

        var commitResult = await _vcs.CommitAsync(_repoDir, "Fix addition bug", "Telechron Repair", "repair@telechron.internal");

        using var repo = new LibGit2Sharp.Repository(_repoDir);
        var headCommit = repo.Head.Tip;
        Assert.Equal(commitResult.CommitReference, headCommit.Sha);
        Assert.NotEqual(snapshot.Value, headCommit.Sha);
    }
}
