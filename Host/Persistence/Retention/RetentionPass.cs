using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Retention;

// R-PER7: archives-then-deletes Run and Finding rows past their retention
// policy. Findings referenced by any RepairAttempt are exempt — that's
// repair-lineage/dedup history R-FIX11's oscillation detection depends on,
// so it is never pruned by this pass (archive it separately if it must ever
// leave the operational DB, which this scaffolding does not yet do).
//
// Archive-before-delete ordering: AppendAsync is awaited and must succeed
// before a row's delete is issued, so a crash mid-pass never loses a row
// that hasn't been archived yet — worst case, a row gets archived twice on
// retry, which is harmless for an append-only JSON Lines sink.
public sealed class RetentionPass(TelechronDbContext db, IRetentionArchive archive, ILogger<RetentionPass> logger)
{
    public async Task<RetentionPassResult> RunRetentionAsync(RetentionPolicy policy, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow - policy.MaxAge;

        // SQLite's EF Core provider cannot translate DateTimeOffset relational
        // comparisons (<, >) in SQL — filter/order client-side instead. A
        // periodic retention pass is a batch job, not a hot path, so
        // materializing the (bounded, already-small-by-design) Runs table is
        // an acceptable tradeoff for correctness here.
        var all = await db.Runs.AsNoTracking().ToListAsync(ct);
        var candidates = all
            .Where(r => r.CompletedAtUtc is not null && r.CompletedAtUtc < cutoff)
            .OrderBy(r => r.CompletedAtUtc)
            .ToList();

        var eligible = candidates.Take(Math.Max(0, all.Count - policy.MaxCount)).ToList();

        foreach (var run in eligible)
        {
            await archive.AppendAsync("Run", JsonSerializer.Serialize(run), ct);
        }

        if (eligible.Count > 0)
        {
            var ids = eligible.Select(r => r.Id).ToHashSet();
            await db.Runs.Where(r => ids.Contains(r.Id)).ExecuteDeleteAsync(ct);
        }

        logger.LogInformation("Retention pass on Run: archived+pruned {Count} rows.", eligible.Count);
        return new RetentionPassResult("Run", eligible.Count, SkippedRepairLineageCount: 0);
    }

    public async Task<RetentionPassResult> FindingRetentionAsync(RetentionPolicy policy, CancellationToken ct = default)
    {
        // See RunRetentionAsync — same SQLite DateTimeOffset translation limitation.
        var cutoff = DateTimeOffset.UtcNow - policy.MaxAge;

        var repairLineageFindingIds = await db.RepairAttemptFindings.AsNoTracking()
            .Select(l => l.FindingId)
            .Distinct()
            .ToListAsync(ct);
        var repairLineageSet = repairLineageFindingIds.ToHashSet();

        var all = await db.Findings.AsNoTracking().ToListAsync(ct);
        var candidates = all
            .Where(f => f.CreatedAtUtc < cutoff)
            .OrderBy(f => f.CreatedAtUtc)
            .ToList();

        var ageEligible = candidates.Take(Math.Max(0, all.Count - policy.MaxCount)).ToList();

        var skipped = ageEligible.Count(f => repairLineageSet.Contains(f.Id));
        var eligible = ageEligible.Where(f => !repairLineageSet.Contains(f.Id)).ToList();

        foreach (var finding in eligible)
        {
            await archive.AppendAsync("Finding", JsonSerializer.Serialize(finding), ct);
        }

        if (eligible.Count > 0)
        {
            var ids = eligible.Select(f => f.Id).ToHashSet();
            await db.Findings.Where(f => ids.Contains(f.Id)).ExecuteDeleteAsync(ct);
        }

        logger.LogInformation(
            "Retention pass on Finding: archived+pruned {Count} rows, exempted {Skipped} repair-lineage rows (R-FIX11).",
            eligible.Count, skipped);
        return new RetentionPassResult("Finding", eligible.Count, skipped);
    }
}
