using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Tests.Phase3;

public sealed class WorkflowRunFindingArtifactTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task WorkflowRun_RoundTrips_WithDefinitionSnapshot_ImmutableFromLiveWorkflow()
    {
        Guid projectId, workflowId;
        using (var scope = _db.CreateScope())
        {
            projectId = await scope.SeedProjectAsync();
            workflowId = await scope.SeedWorkflowAsync(projectId);
        }

        var snapshot = """{"steps":["RunTests"],"pinnedFunctionVersion":"1.0"}""";
        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            Status = WorkflowRunStatus.Running,
            DefinitionSnapshotJson = snapshot,
            StartedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IWorkflowRunRepository>().AddAsync(run);

        // Now mutate the live Workflow's definition — the WorkflowRun's
        // snapshot must NOT reflect this (R-DM5 Definition Pinning).
        using (var scope = _db.CreateScope())
        {
            var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowRepository>();
            var liveWorkflow = await workflows.GetByIdAsync(workflowId);
            var mutated = liveWorkflow! with { DefinitionJson = """{"steps":["RunTests","NewStep"]}""" };
            await workflows.UpdateAsync(mutated);
        }

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IWorkflowRunRepository>();
        var loaded = await repo.GetByIdAsync(run.Id);

        Assert.NotNull(loaded);
        Assert.Equal(snapshot, loaded.DefinitionSnapshotJson); // unchanged despite the live Workflow edit
        Assert.Equal(WorkflowRunStatus.Running, loaded.Status);

        var byWorkflow = await repo.GetByWorkflowAsync(workflowId);
        Assert.Contains(byWorkflow, r => r.Id == run.Id);

        var active = await repo.GetActiveAsync();
        Assert.Contains(active, r => r.Id == run.Id);
    }

    [Fact]
    public async Task Finding_RoundTrips_WithRunProvenance_AndFailureClass()
    {
        Guid projectId, runId;
        using (var scope = _db.CreateScope())
        {
            projectId = await scope.SeedProjectAsync();
            runId = await scope.SeedRunAsync(projectId);
        }

        var finding = new Finding
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RunId = runId,
            OriginFilePath = "src/Foo.cs",
            RootCauseSignature = "NullReferenceException at Foo.Bar",
            Severity = FindingSeverity.Critical,
            Category = "test failure",
            FailureClass = FindingFailureClass.Code,
            Fixability = "likely-fixable",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IFindingRepository>().AddAsync(finding);

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IFindingRepository>();
        var loaded = await repo.GetByIdAsync(finding.Id);

        Assert.NotNull(loaded);
        Assert.Equal(FindingFailureClass.Code, loaded.FailureClass);
        Assert.Equal(runId, loaded.RunId);

        var byRun = await repo.GetByRunAsync(runId);
        Assert.Contains(byRun, f => f.Id == finding.Id);
    }

    [Fact]
    public async Task Finding_EnvironmentFailureClass_IsDistinguishableFromCode()
    {
        Guid projectId;
        using (var scope = _db.CreateScope())
            projectId = await scope.SeedProjectAsync();

        var envFinding = new Finding
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RootCauseSignature = "Run Stalled — agent heartbeat lost",
            Severity = FindingSeverity.Warning,
            Category = "infra",
            FailureClass = FindingFailureClass.Environment,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IFindingRepository>().AddAsync(envFinding);

        using var verifyScope = _db.CreateScope();
        var loaded = await verifyScope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(envFinding.Id);

        Assert.NotNull(loaded);
        Assert.Equal(FindingFailureClass.Environment, loaded.FailureClass);
    }

    [Fact]
    public async Task Artifact_RoundTrips_WithWorkflowRunLink_NoBinaryPayloadField()
    {
        Guid projectId, workflowId, workflowRunId;
        using (var scope = _db.CreateScope())
        {
            projectId = await scope.SeedProjectAsync();
            workflowId = await scope.SeedWorkflowAsync(projectId);
            workflowRunId = await scope.SeedWorkflowRunAsync(workflowId);
        }

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = workflowRunId,
            Name = "test-report.json",
            ArtifactType = "Report",
            BlobRef = "ab/cd/some-blob-ref.json",
            SizeBytes = 4096,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IArtifactRepository>().AddAsync(artifact);

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IArtifactRepository>();
        var loaded = await repo.GetByIdAsync(artifact.Id);

        Assert.NotNull(loaded);
        Assert.Equal("ab/cd/some-blob-ref.json", loaded.BlobRef);
        Assert.Equal(4096, loaded.SizeBytes);

        var byWorkflowRun = await repo.GetByWorkflowRunAsync(workflowRunId);
        Assert.Contains(byWorkflowRun, a => a.Id == artifact.Id);
    }

    [Fact]
    public async Task Artifact_WorkflowRunId_CanBeNull()
    {
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = null,
            Name = "standalone.json",
            ArtifactType = "Json",
            BlobRef = "ef/01/standalone.json",
            SizeBytes = 10,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IArtifactRepository>().AddAsync(artifact);

        using var verifyScope = _db.CreateScope();
        var loaded = await verifyScope.ServiceProvider.GetRequiredService<IArtifactRepository>().GetByIdAsync(artifact.Id);

        Assert.NotNull(loaded);
        Assert.Null(loaded.WorkflowRunId);
    }
}
