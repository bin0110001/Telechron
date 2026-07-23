using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Tests.Phase3;

public sealed class RepairAttemptTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task RepairAttempt_RoundTrips_WithSingleFinding()
    {
        Guid projectId, findingId;
        using (var scope = _db.CreateScope())
        {
            projectId = await scope.SeedProjectAsync();
            findingId = await scope.SeedFindingAsync(projectId);
        }

        var attempt = new RepairAttempt
        {
            Id = Guid.NewGuid(),
            FindingIds = [findingId],
            SnapshotRef = "snapshot/abc123",
            PatchDiff = "--- a/Foo.cs\n+++ b/Foo.cs\n@@ -1 +1 @@\n-bad\n+good\n",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>().AddAsync(attempt);

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>();
        var loaded = await repo.GetByIdAsync(attempt.Id);

        Assert.NotNull(loaded);
        Assert.Single(loaded.FindingIds);
        Assert.Equal(findingId, loaded.FindingIds[0]);
        Assert.Null(loaded.ApprovalDecision);
    }

    [Fact]
    public async Task RepairAttempt_RoundTrips_WithMultipleFindings_BundledPatch()
    {
        Guid projectId, findingA, findingB;
        using (var scope = _db.CreateScope())
        {
            projectId = await scope.SeedProjectAsync();
            findingA = await scope.SeedFindingAsync(projectId);
            findingB = await scope.SeedFindingAsync(projectId);
        }

        var attempt = new RepairAttempt
        {
            Id = Guid.NewGuid(),
            FindingIds = [findingA, findingB],
            SnapshotRef = "snapshot/bundle",
            PatchDiff = "multi-file diff",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>().AddAsync(attempt);

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>();
        var loaded = await repo.GetByIdAsync(attempt.Id);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.FindingIds.Count);
        Assert.Contains(findingA, loaded.FindingIds);
        Assert.Contains(findingB, loaded.FindingIds);
    }

    [Fact]
    public async Task RepairAttempt_QueryableByFinding_AcrossMultipleAttempts()
    {
        // R-DM3a: "a Finding may accrue several attempts" must be queryable.
        Guid projectId, findingId;
        using (var scope = _db.CreateScope())
        {
            projectId = await scope.SeedProjectAsync();
            findingId = await scope.SeedFindingAsync(projectId);
        }

        var firstAttempt = new RepairAttempt
        {
            Id = Guid.NewGuid(),
            FindingIds = [findingId],
            SnapshotRef = "snapshot/1",
            PatchDiff = "first attempt diff",
            ApprovalDecision = RepairApprovalDecision.Rejected,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        var secondAttempt = new RepairAttempt
        {
            Id = Guid.NewGuid(),
            FindingIds = [findingId],
            SnapshotRef = "snapshot/2",
            PatchDiff = "second attempt diff",
            ApprovalDecision = RepairApprovalDecision.Approved,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>();
            await repo.AddAsync(firstAttempt);
            await repo.AddAsync(secondAttempt);
        }

        using var verifyScope = _db.CreateScope();
        var byFinding = await verifyScope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>().GetByFindingAsync(findingId);

        Assert.Equal(2, byFinding.Count);
        Assert.Contains(byFinding, a => a.Id == firstAttempt.Id && a.ApprovalDecision == RepairApprovalDecision.Rejected);
        Assert.Contains(byFinding, a => a.Id == secondAttempt.Id && a.ApprovalDecision == RepairApprovalDecision.Approved);
    }

    [Fact]
    public async Task RepairAttempt_WithApproverAndResultingArtifact_RoundTrips()
    {
        Guid projectId, findingId, approverUserId, workflowId, workflowRunId, artifactId;
        using (var scope = _db.CreateScope())
        {
            projectId = await scope.SeedProjectAsync();
            findingId = await scope.SeedFindingAsync(projectId);
            approverUserId = await scope.SeedUserAsync();
            workflowId = await scope.SeedWorkflowAsync(projectId);
            workflowRunId = await scope.SeedWorkflowRunAsync(workflowId);
        }

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = workflowRunId,
            Name = "committed-diff.patch",
            ArtifactType = "SourceCode",
            BlobRef = "aa/bb/committed-diff.patch",
            SizeBytes = 128,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IArtifactRepository>().AddAsync(artifact);
        artifactId = artifact.Id;

        var attempt = new RepairAttempt
        {
            Id = Guid.NewGuid(),
            FindingIds = [findingId],
            SnapshotRef = "snapshot/approved",
            PatchDiff = "approved diff",
            VerifyResultJson = """{"passed":true}""",
            ApprovalDecision = RepairApprovalDecision.Approved,
            ApproverUserId = approverUserId,
            ResultingArtifactId = artifactId,
            CommitReference = "abc123def456",
            ProvenanceRecordJson = """{"signed":true}""",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>().AddAsync(attempt);

        using var verifyScope = _db.CreateScope();
        var loaded = await verifyScope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>().GetByIdAsync(attempt.Id);

        Assert.NotNull(loaded);
        Assert.Equal(approverUserId, loaded.ApproverUserId);
        Assert.Equal(artifactId, loaded.ResultingArtifactId);
        Assert.Equal("abc123def456", loaded.CommitReference);
        // The full chain Artifact/commit -> RepairAttempt -> Finding(s) -> Run
        // is queryable via these FKs, per R-DM3a's "show me why this line changed."
    }
}
