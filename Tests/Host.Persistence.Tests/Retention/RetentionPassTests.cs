using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Telechron.Host.Persistence.Retention;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Host.Persistence.Tests.Phase3;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Tests.Retention;

public sealed class RetentionPassTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;
    private string _archiveRoot = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        _archiveRoot = Path.Combine(Path.GetTempPath(), "telechron-tests", "retention-archive-" + Guid.NewGuid().ToString("N"));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_archiveRoot, recursive: true); } catch { /* best-effort */ }
    }

    private RetentionPass CreatePass(IServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<TelechronDbContext>(),
        new FilesystemRetentionArchive(_archiveRoot),
        NullLogger<RetentionPass>.Instance);

    private async Task<Guid> SeedCompletedRunAsync(IServiceScope scope, Guid projectId, DateTimeOffset completedAtUtc)
    {
        var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var run = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Status = RunStatus.Passed,
            StartedAtUtc = completedAtUtc - TimeSpan.FromMinutes(5),
            CompletedAtUtc = completedAtUtc,
        };
        await runs.AddAsync(run);
        return run.Id;
    }

    [Fact]
    public async Task RunRetention_PrunesRowsOlderThanMaxAge_WhenOverMaxCount()
    {
        Guid projectId;
        using (var scope = _db.CreateScope())
            projectId = await scope.SeedProjectAsync();

        var oldRunId = await SeedOneRun(projectId, DateTimeOffset.UtcNow - TimeSpan.FromDays(200));
        var recentRunId = await SeedOneRun(projectId, DateTimeOffset.UtcNow - TimeSpan.FromDays(1));

        using (var scope = _db.CreateScope())
        {
            var pass = CreatePass(scope);
            // MaxCount: 1 forces the old row to be pruned even though only 2 rows exist.
            var result = await pass.RunRetentionAsync(new RetentionPolicy(TimeSpan.FromDays(90), MaxCount: 1));
            Assert.Equal(1, result.ArchivedCount);
        }

        using var verifyScope = _db.CreateScope();
        var runs = verifyScope.ServiceProvider.GetRequiredService<IRunRepository>();
        Assert.Null(await runs.GetByIdAsync(oldRunId));
        Assert.NotNull(await runs.GetByIdAsync(recentRunId));
    }

    private async Task<Guid> SeedOneRun(Guid projectId, DateTimeOffset completedAtUtc)
    {
        using var scope = _db.CreateScope();
        return await SeedCompletedRunAsync(scope, projectId, completedAtUtc);
    }

    [Fact]
    public async Task RunRetention_KeepsRowsWithinMaxCount_EvenIfOlderThanMaxAge()
    {
        Guid projectId;
        using (var scope = _db.CreateScope())
            projectId = await scope.SeedProjectAsync();

        // Both rows are "old" by age, but MaxCount is generous (10) — count-based
        // retention must not prune below MaxCount regardless of age.
        await SeedOneRun(projectId, DateTimeOffset.UtcNow - TimeSpan.FromDays(200));
        await SeedOneRun(projectId, DateTimeOffset.UtcNow - TimeSpan.FromDays(150));

        using (var scope = _db.CreateScope())
        {
            var pass = CreatePass(scope);
            var result = await pass.RunRetentionAsync(new RetentionPolicy(TimeSpan.FromDays(90), MaxCount: 10));
            Assert.Equal(0, result.ArchivedCount);
        }

        using var verifyScope = _db.CreateScope();
        var remaining = await verifyScope.ServiceProvider.GetRequiredService<TelechronDbContext>().Runs.CountAsync();
        Assert.Equal(2, remaining);
    }

    [Fact]
    public async Task RunRetention_ArchivesRowBeforeDeleting_ArchiveContainsRowData()
    {
        Guid projectId;
        using (var scope = _db.CreateScope())
            projectId = await scope.SeedProjectAsync();

        var oldRunId = await SeedOneRun(projectId, DateTimeOffset.UtcNow - TimeSpan.FromDays(200));

        using (var scope = _db.CreateScope())
        {
            var pass = CreatePass(scope);
            await pass.RunRetentionAsync(new RetentionPolicy(TimeSpan.FromDays(90), MaxCount: 0));
        }

        var archiveFiles = Directory.GetFiles(_archiveRoot, "*.jsonl", SearchOption.AllDirectories);
        Assert.NotEmpty(archiveFiles);
        var content = await File.ReadAllTextAsync(archiveFiles.Single());
        Assert.Contains(oldRunId.ToString(), content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FindingRetention_ExemptsRepairLineageFindings_EvenWhenOldAndOverCount()
    {
        Guid projectId, lineageFindingId, ordinaryFindingId;
        using (var scope = _db.CreateScope())
        {
            projectId = await scope.SeedProjectAsync();
            lineageFindingId = await scope.SeedFindingAsync(projectId);
            ordinaryFindingId = await scope.SeedFindingAsync(projectId);
        }

        // Age both Findings past the cutoff directly (SeedFindingAsync stamps "now").
        using (var scope = _db.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TelechronDbContext>();
            var oldTimestamp = DateTimeOffset.UtcNow - TimeSpan.FromDays(400);
            await db.Findings.Where(f => f.Id == lineageFindingId || f.Id == ordinaryFindingId)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.CreatedAtUtc, oldTimestamp));

            // Link lineageFindingId to a RepairAttempt — this is what R-FIX11
            // repair-lineage exemption keys off.
            var repairAttempts = scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>();
            await repairAttempts.AddAsync(new RepairAttempt
            {
                Id = Guid.NewGuid(),
                FindingIds = [lineageFindingId],
                SnapshotRef = "snapshot/x",
                PatchDiff = "diff",
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        using (var scope = _db.CreateScope())
        {
            var pass = CreatePass(scope);
            var result = await pass.FindingRetentionAsync(new RetentionPolicy(TimeSpan.FromDays(90), MaxCount: 0));

            Assert.Equal(1, result.ArchivedCount); // only the ordinary Finding
            Assert.Equal(1, result.SkippedRepairLineageCount);
        }

        using var verifyScope = _db.CreateScope();
        var findings = verifyScope.ServiceProvider.GetRequiredService<IFindingRepository>();
        Assert.NotNull(await findings.GetByIdAsync(lineageFindingId)); // survives — repair lineage
        Assert.Null(await findings.GetByIdAsync(ordinaryFindingId)); // pruned — ordinary
    }
}
