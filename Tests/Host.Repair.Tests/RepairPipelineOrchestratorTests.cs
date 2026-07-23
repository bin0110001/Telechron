using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Host.Persistence.Tests.Phase3;
using Telechron.Host.Repair.Tests.Fixtures;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules.Runners;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Repair;

namespace Telechron.Host.Repair.Tests;

// Exercises the whole R-NS2/R-FIX2 pipeline against REAL git
// (GitRepairVersionControl/LibGit2Sharp), REAL SQLite persistence
// (RepairAttemptRepository), REAL gate evaluators (privileged-path,
// diff-scope, oscillation), REAL governor/concurrency gate, and a REAL
// ECDSA provenance signer. Only Verify and the LLM fix path are faked --
// those need a real container runtime / real LLM, covered by a separate
// live test using the actual Phase 4/Phase 6 infrastructure.
public sealed class RepairPipelineOrchestratorTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;
    private string _repoDir = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        _repoDir = Path.Combine(Path.GetTempPath(), "telechron-orchestrator-" + Guid.NewGuid());
        Directory.CreateDirectory(_repoDir);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        if (Directory.Exists(_repoDir))
        {
            // Git repos leave read-only files under .git/objects on some
            // platforms -- clear attributes before delete so cleanup
            // doesn't fail (same pattern as GitRepairVersionControlTests).
            foreach (var file in Directory.GetFiles(_repoDir, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_repoDir, recursive: true);
        }
    }

    private RepairPipelineOrchestrator BuildOrchestrator(
        IServiceProvider services,
        ILlmFixGenerator llmFixGenerator,
        IRepairVerifier verifier,
        IArchitecturalDriftDetector? driftDetector = null) =>
        new(
            versionControl: new GitRepairVersionControl(NullLogger<GitRepairVersionControl>.Instance),
            governor: new RepairAttemptGovernor(services.GetRequiredService<IRepairAttemptRepository>()),
            concurrencyGate: new RepairConcurrencyGate(),
            deterministicFixProvider: new CompositeDeterministicFixProvider(),
            llmFixGenerator: llmFixGenerator,
            verifier: verifier,
            privilegedPathGuard: new PrivilegedPathGuard(),
            diffScopeGuard: new RepairDiffScopeGuard(),
            oscillationDetector: new OscillationDetector(),
            driftDetector: driftDetector ?? new FakeArchitecturalDriftDetector(),
            provenanceSigner: new RepairProvenanceSigner(Microsoft.Extensions.Options.Options.Create(new RepairProvenanceSignerOptions())),
            repairAttemptRepository: services.GetRequiredService<IRepairAttemptRepository>());

    private void WriteSourceFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_repoDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private static PatchDiff MakePatch(string relativePath, string unifiedDiff) =>
        new([new PatchFileChange(relativePath, unifiedDiff)]);

    [Fact]
    public async Task RunAsync_FullyAutonomousPolicy_VerifiedCleanPatch_CommitsWithSignedProvenance()
    {
        WriteSourceFile("Foo.cs", "class Foo { int Bar() => 1; }\n");

        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingId = await scope.SeedFindingAsync(projectId);
        var findings = new List<Finding>
        {
            (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingId))!,
        };

        var patch = MakePatch("Foo.cs",
            "--- a/Foo.cs\n+++ b/Foo.cs\n@@ -1 +1 @@\n-class Foo { int Bar() => 1; }\n+class Foo { int Bar() => 2; }\n");

        var orchestrator = BuildOrchestrator(scope.ServiceProvider, FakeLlmFixGenerator.ProducingPatch(patch), FakeRepairVerifier.Succeeding());

        var request = new RepairRequest(projectId, _repoDir, RepairPolicy.FullyAutonomous, findings, [], null);
        var outcome = await orchestrator.RunAsync(request);

        Assert.Equal(RepairOutcomeStatus.Committed, outcome.Status);
        Assert.NotNull(outcome.RepairAttemptId);

        var stored = await scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>().GetByIdAsync(outcome.RepairAttemptId!.Value);
        Assert.NotNull(stored);
        Assert.NotNull(stored.CommitReference);
        Assert.NotNull(stored.ProvenanceRecordJson);
        Assert.Contains("SignatureBase64", stored.ProvenanceRecordJson);

        var newContent = File.ReadAllText(Path.Combine(_repoDir, "Foo.cs"));
        Assert.Contains("Bar() => 2", newContent);
    }

    [Fact]
    public async Task RunAsync_RequireApprovalPolicy_VerifiedCleanPatch_PausesWithoutCommitting()
    {
        WriteSourceFile("Foo.cs", "original content\n");

        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingId = await scope.SeedFindingAsync(projectId);
        var finding = (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingId))!;

        var patch = MakePatch("Foo.cs", "--- a/Foo.cs\n+++ b/Foo.cs\n@@ -1 +1 @@\n-original content\n+patched content\n");
        var orchestrator = BuildOrchestrator(scope.ServiceProvider, FakeLlmFixGenerator.ProducingPatch(patch), FakeRepairVerifier.Succeeding());

        var request = new RepairRequest(projectId, _repoDir, RepairPolicy.RequireApproval, [finding], [], null);
        var outcome = await orchestrator.RunAsync(request);

        Assert.Equal(RepairOutcomeStatus.PendingApproval, outcome.Status);

        var stored = await scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>().GetByIdAsync(outcome.RepairAttemptId!.Value);
        Assert.Null(stored!.CommitReference);

        // Working tree is left AS-APPLIED for human review, not reverted.
        Assert.Contains("patched content", File.ReadAllText(Path.Combine(_repoDir, "Foo.cs")));
    }

    [Fact]
    public async Task RunAsync_VerifyFails_RevertsWorkingTreeToSnapshot()
    {
        WriteSourceFile("Foo.cs", "original content\n");

        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingId = await scope.SeedFindingAsync(projectId);
        var finding = (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingId))!;

        var patch = MakePatch("Foo.cs", "--- a/Foo.cs\n+++ b/Foo.cs\n@@ -1 +1 @@\n-original content\n+broken content\n");
        var orchestrator = BuildOrchestrator(scope.ServiceProvider, FakeLlmFixGenerator.ProducingPatch(patch), FakeRepairVerifier.Failing());

        var request = new RepairRequest(projectId, _repoDir, RepairPolicy.FullyAutonomous, [finding], [], null);
        var outcome = await orchestrator.RunAsync(request);

        Assert.Equal(RepairOutcomeStatus.Reverted, outcome.Status);
        Assert.Equal("original content\n", File.ReadAllText(Path.Combine(_repoDir, "Foo.cs")).Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task RunAsync_LlmRequiresSynthesis_PausesForApproval_NeverTouchesWorkingTree()
    {
        WriteSourceFile("Foo.cs", "original content\n");

        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingId = await scope.SeedFindingAsync(projectId);
        var finding = (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingId))!;

        var verifier = FakeRepairVerifier.Succeeding();
        var orchestrator = BuildOrchestrator(scope.ServiceProvider, FakeLlmFixGenerator.RequiringSynthesis(), verifier);

        // FullyAutonomous policy -- R-FIX10/R-NS3 must still force approval.
        var request = new RepairRequest(projectId, _repoDir, RepairPolicy.FullyAutonomous, [finding], [], null);
        var outcome = await orchestrator.RunAsync(request);

        Assert.Equal(RepairOutcomeStatus.PendingApproval, outcome.Status);
        Assert.Contains(outcome.ForcedApprovalReasons, r => r.Contains("Capability Synthesis"));
        Assert.Equal(0, verifier.CallCount);
        Assert.Equal("original content\n", File.ReadAllText(Path.Combine(_repoDir, "Foo.cs")).Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task RunAsync_PrivilegedPathPatch_ForcesApproval_EvenUnderFullyAutonomous()
    {
        WriteSourceFile("Sdk/Repair/RepairPipelineOrchestrator.cs", "// original\n");

        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingId = await scope.SeedFindingAsync(projectId);
        var finding = (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingId))!;

        var patch = MakePatch("Sdk/Repair/RepairPipelineOrchestrator.cs",
            "--- a/Sdk/Repair/RepairPipelineOrchestrator.cs\n+++ b/Sdk/Repair/RepairPipelineOrchestrator.cs\n@@ -1 +1 @@\n-// original\n+// changed\n");
        var orchestrator = BuildOrchestrator(scope.ServiceProvider, FakeLlmFixGenerator.ProducingPatch(patch), FakeRepairVerifier.Succeeding());

        var request = new RepairRequest(projectId, _repoDir, RepairPolicy.FullyAutonomous, [finding], [], null);
        var outcome = await orchestrator.RunAsync(request);

        Assert.Equal(RepairOutcomeStatus.PendingApproval, outcome.Status);
        Assert.Contains(outcome.ForcedApprovalReasons, r => r.Contains("Privileged-path"));
    }

    [Fact]
    public async Task RunAsync_OversizedPatch_ForcesApproval_EvenUnderFullyAutonomous()
    {
        WriteSourceFile("Foo.cs", "original\n");

        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingId = await scope.SeedFindingAsync(projectId);
        var finding = (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingId))!;

        var manyFiles = Enumerable.Range(0, 10)
            .Select(i => new PatchFileChange($"File{i}.cs", $"--- a/File{i}.cs\n+++ b/File{i}.cs\n@@ -0,0 +1 @@\n+// new file {i}\n"))
            .ToList();
        var oversizedPatch = new PatchDiff(manyFiles);

        var orchestrator = BuildOrchestrator(scope.ServiceProvider, FakeLlmFixGenerator.ProducingPatch(oversizedPatch), FakeRepairVerifier.Succeeding());

        var request = new RepairRequest(projectId, _repoDir, RepairPolicy.FullyAutonomous, [finding], [], null);
        var outcome = await orchestrator.RunAsync(request);

        Assert.Equal(RepairOutcomeStatus.PendingApproval, outcome.Status);
        Assert.Contains(outcome.ForcedApprovalReasons, r => r.Contains("Diff scope limit"));
    }

    [Fact]
    public async Task RunAsync_DriftDetected_ForcesApproval_EvenUnderFullyAutonomous()
    {
        WriteSourceFile("Foo.cs", "original\n");

        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingId = await scope.SeedFindingAsync(projectId);
        var finding = (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingId))!;

        var patch = MakePatch("Foo.cs", "--- a/Foo.cs\n+++ b/Foo.cs\n@@ -1 +1 @@\n-original\n+changed\n");
        var orchestrator = BuildOrchestrator(
            scope.ServiceProvider, FakeLlmFixGenerator.ProducingPatch(patch), FakeRepairVerifier.Succeeding(),
            driftDetector: new FakeArchitecturalDriftDetector(isDrift: true));

        var requirement = new Requirement
        {
            Id = Guid.NewGuid(),
            DesignDocumentId = Guid.NewGuid(),
            RequirementId = "R-TEST1",
            Title = "Test requirement",
            Body = "Foo must always return the original value.",
            Status = RequirementStatus.Active,
            CurrentRevisionNumber = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        var request = new RepairRequest(projectId, _repoDir, RepairPolicy.FullyAutonomous, [finding], [requirement], null);
        var outcome = await orchestrator.RunAsync(request);

        Assert.Equal(RepairOutcomeStatus.PendingApproval, outcome.Status);
        Assert.Contains(outcome.ForcedApprovalReasons, r => r.Contains("Architectural drift"));
    }

    [Fact]
    public async Task RunAsync_NoFixProduced_WhenLlmDeclines()
    {
        WriteSourceFile("Foo.cs", "original\n");

        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingId = await scope.SeedFindingAsync(projectId);
        var finding = (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingId))!;

        var orchestrator = BuildOrchestrator(scope.ServiceProvider, FakeLlmFixGenerator.Declining(), FakeRepairVerifier.Succeeding());

        var request = new RepairRequest(projectId, _repoDir, RepairPolicy.FullyAutonomous, [finding], [], null);
        var outcome = await orchestrator.RunAsync(request);

        Assert.Equal(RepairOutcomeStatus.NoFixProduced, outcome.Status);
        Assert.Null(outcome.RepairAttemptId);
    }

    [Fact]
    public async Task RunAsync_AttemptCapExceeded_GovernanceDeclines_NeverCallsLlm()
    {
        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingId = await scope.SeedFindingAsync(projectId);
        var finding = (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingId))!;

        var repairAttemptRepository = scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>();
        for (var i = 0; i < 5; i++)
        {
            await repairAttemptRepository.AddAsync(new RepairAttempt
            {
                Id = Guid.NewGuid(),
                FindingIds = [findingId],
                SnapshotRef = $"snapshot/{i}",
                PatchDiff = "prior diff",
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        WriteSourceFile("Foo.cs", "original\n");
        var llmFixGenerator = FakeLlmFixGenerator.ProducingPatch(MakePatch("Foo.cs", "irrelevant"));
        var orchestrator = BuildOrchestrator(scope.ServiceProvider, llmFixGenerator, FakeRepairVerifier.Succeeding());

        var request = new RepairRequest(projectId, _repoDir, RepairPolicy.FullyAutonomous, [finding], [], null);
        var outcome = await orchestrator.RunAsync(request);

        Assert.Equal(RepairOutcomeStatus.GovernanceDeclined, outcome.Status);
        Assert.Equal(0, llmFixGenerator.CallCount);
    }

    [Fact]
    public async Task RunAsync_MultipleFindingsBundledAsRepairPlan_OneApprovalGateCoversAllFindings()
    {
        WriteSourceFile("Foo.cs", "original\n");
        WriteSourceFile("Bar.cs", "original\n");

        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingRepo = scope.ServiceProvider.GetRequiredService<IFindingRepository>();
        var findingAId = await scope.SeedFindingAsync(projectId);
        var findingBId = await scope.SeedFindingAsync(projectId);
        var findings = new List<Finding>
        {
            (await findingRepo.GetByIdAsync(findingAId))!,
            (await findingRepo.GetByIdAsync(findingBId))!,
        };

        var patch = new PatchDiff([
            new PatchFileChange("Foo.cs", "--- a/Foo.cs\n+++ b/Foo.cs\n@@ -1 +1 @@\n-original\n+fixed A\n"),
            new PatchFileChange("Bar.cs", "--- a/Bar.cs\n+++ b/Bar.cs\n@@ -1 +1 @@\n-original\n+fixed B\n"),
        ]);
        var orchestrator = BuildOrchestrator(scope.ServiceProvider, FakeLlmFixGenerator.ProducingPatch(patch), FakeRepairVerifier.Succeeding());

        var request = new RepairRequest(projectId, _repoDir, RepairPolicy.RequireApproval, findings, [], null);
        var outcome = await orchestrator.RunAsync(request);

        Assert.Equal(RepairOutcomeStatus.PendingApproval, outcome.Status);
        var stored = await scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>().GetByIdAsync(outcome.RepairAttemptId!.Value);
        Assert.Equal(2, stored!.FindingIds.Count);
        Assert.Contains(findingAId, stored.FindingIds);
        Assert.Contains(findingBId, stored.FindingIds);
    }

    [Fact]
    public async Task RunAsync_ConcurrentAttemptsOnSameProject_AreQueuedNotInterleaved()
    {
        WriteSourceFile("Foo.cs", "original\n");

        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingId = await scope.SeedFindingAsync(projectId);
        var finding = (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingId))!;

        var patch = MakePatch("Foo.cs", "--- a/Foo.cs\n+++ b/Foo.cs\n@@ -1 +1 @@\n-original\n+concurrent\n");

        var verifier = new SlowFakeVerifier();
        var orchestrator = BuildOrchestrator(scope.ServiceProvider, FakeLlmFixGenerator.ProducingPatch(patch), verifier);

        var request = new RepairRequest(projectId, _repoDir, RepairPolicy.RequireApproval, [finding], [], null);

        var task1 = orchestrator.RunAsync(request);
        var task2 = orchestrator.RunAsync(request);
        await Task.WhenAll(task1, task2);

        // R-FIX9: the concurrency gate serializes these -- verify's max
        // concurrent call count observed must never exceed 1.
        Assert.Equal(1, verifier.MaxConcurrentCalls);
    }

    private sealed class SlowFakeVerifier : IRepairVerifier
    {
        private int _current;
        public int MaxConcurrentCalls { get; private set; }
        private readonly Lock _lock = new();

        public async Task<Sdk.Repair.VerifyResult> VerifyAsync(string projectRootPath, CancellationToken ct = default)
        {
            lock (_lock)
            {
                _current++;
                MaxConcurrentCalls = Math.Max(MaxConcurrentCalls, _current);
            }

            await Task.Delay(200, ct);

            lock (_lock) { _current--; }

            return new Sdk.Repair.VerifyResult(true, new TestRunResult(true, [], [], "ok"), "ok");
        }
    }
}
