using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Host.Persistence.Tests.Phase3;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Repair;

namespace Telechron.Host.Repair.Tests;

// Real SQLite-backed RepairAttempt history -- proves R-FIX3's attempt cap
// is actually self-accounting off persisted rows, not an in-memory counter
// that would reset across process restarts.
public sealed class RepairAttemptGovernorTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task CheckAsync_NoPriorAttempts_IsNotDeclined()
    {
        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingId = await scope.SeedFindingAsync(projectId);
        var finding = (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingId))!;

        var governor = new RepairAttemptGovernor(scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>());

        var result = await governor.CheckAsync(projectId, [finding]);

        Assert.False(result.Declined);
    }

    [Fact]
    public async Task CheckAsync_AttemptsBelowCap_IsNotDeclined()
    {
        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingId = await scope.SeedFindingAsync(projectId);
        var finding = (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingId))!;
        var repo = scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>();

        for (var i = 0; i < 4; i++)
        {
            await repo.AddAsync(new RepairAttempt
            {
                Id = Guid.NewGuid(),
                FindingIds = [findingId],
                SnapshotRef = $"snapshot/{i}",
                PatchDiff = "diff",
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        var governor = new RepairAttemptGovernor(repo, new RepairAttemptGovernorOptions(MaxAttemptsPerFinding: 5));

        var result = await governor.CheckAsync(projectId, [finding]);

        Assert.False(result.Declined);
    }

    [Fact]
    public async Task CheckAsync_AttemptsAtCap_IsDeclined()
    {
        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingId = await scope.SeedFindingAsync(projectId);
        var finding = (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingId))!;
        var repo = scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>();

        for (var i = 0; i < 5; i++)
        {
            await repo.AddAsync(new RepairAttempt
            {
                Id = Guid.NewGuid(),
                FindingIds = [findingId],
                SnapshotRef = $"snapshot/{i}",
                PatchDiff = "diff",
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        var governor = new RepairAttemptGovernor(repo, new RepairAttemptGovernorOptions(MaxAttemptsPerFinding: 5));

        var result = await governor.CheckAsync(projectId, [finding]);

        Assert.True(result.Declined);
    }

    [Fact]
    public async Task CheckAsync_UnrelatedFindingsAttempts_DoNotCountTowardThisFindingsCap()
    {
        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var findingA = await scope.SeedFindingAsync(projectId);
        var findingB = await scope.SeedFindingAsync(projectId);
        var repo = scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>();

        for (var i = 0; i < 5; i++)
        {
            await repo.AddAsync(new RepairAttempt
            {
                Id = Guid.NewGuid(),
                FindingIds = [findingA],
                SnapshotRef = $"snapshot/{i}",
                PatchDiff = "diff",
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        var findingBDomain = (await scope.ServiceProvider.GetRequiredService<IFindingRepository>().GetByIdAsync(findingB))!;
        var governor = new RepairAttemptGovernor(repo, new RepairAttemptGovernorOptions(MaxAttemptsPerFinding: 5));

        var result = await governor.CheckAsync(projectId, [findingBDomain]);

        Assert.False(result.Declined);
    }
}
