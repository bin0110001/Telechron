using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Telechron.Sdk.Security.Audit;

namespace Telechron.Host.Security.Audit;

// R-SEC7: hash-chained append. RecordHash = SHA256(PriorHash || canonical
// fields), so any edit/delete/reorder of a stored row breaks the chain at
// that point, detectable by VerifyChainAsync without needing a separate
// signing key — the chain's integrity comes from each record committing to
// its predecessor, not from a detached signature.
//
// A process-wide semaphore serializes Append across concurrent callers within
// this Host instance so PriorHash always reflects the true immediately-prior
// record (EF's SaveChanges alone doesn't prevent two concurrent appends from
// both reading the same "last" row before either commits).
public sealed class SqliteAuditLog(AuditDbContext db) : IAuditLog
{
    private static readonly SemaphoreSlim AppendLock = new(1, 1);
    // SHA-256 output length (64 hex chars) — the sentinel PriorHash for the
    // first record in the chain.
    private static readonly string GenesisHash = new('0', 64);

    public async Task<AuditEvent> AppendAsync(
        AuditEventKind kind, string detailJson, Guid? actorUserId = null, Guid? projectId = null, CancellationToken ct = default)
    {
        await AppendLock.WaitAsync(ct);
        try
        {
            var prior = await db.AuditEvents.OrderByDescending(a => a.Sequence).FirstOrDefaultAsync(ct);
            var priorHash = prior?.RecordHash ?? GenesisHash;
            var occurredAtUtc = DateTimeOffset.UtcNow;

            var entity = new AuditEventEntity
            {
                Kind = (int)kind,
                OccurredAtUtc = occurredAtUtc,
                ActorUserId = actorUserId,
                ProjectId = projectId,
                DetailJson = detailJson,
                PriorHash = priorHash,
            };
            entity.RecordHash = ComputeHash(priorHash, entity);

            db.AuditEvents.Add(entity);
            await db.SaveChangesAsync(ct);

            return ToDomain(entity);
        }
        finally
        {
            AppendLock.Release();
        }
    }

    public async Task<IReadOnlyList<AuditEvent>> ReadAsync(long fromSequence = 0, int limit = 100, CancellationToken ct = default)
    {
        var entities = await db.AuditEvents
            .Where(a => a.Sequence >= fromSequence)
            .OrderBy(a => a.Sequence)
            .Take(limit)
            .ToListAsync(ct);
        return entities.Select(ToDomain).ToList();
    }

    public async Task<AuditVerificationResult> VerifyChainAsync(CancellationToken ct = default)
    {
        var expectedPriorHash = GenesisHash;
        await foreach (var entity in db.AuditEvents.AsNoTracking().OrderBy(a => a.Sequence).AsAsyncEnumerable().WithCancellation(ct))
        {
            if (!string.Equals(entity.PriorHash, expectedPriorHash, StringComparison.Ordinal))
                return new AuditVerificationResult(IsIntact: false, entity.Sequence);

            var recomputed = ComputeHash(entity.PriorHash, entity);
            if (!string.Equals(entity.RecordHash, recomputed, StringComparison.Ordinal))
                return new AuditVerificationResult(IsIntact: false, entity.Sequence);

            expectedPriorHash = entity.RecordHash;
        }

        return new AuditVerificationResult(IsIntact: true, FirstTamperedSequence: null);
    }

    private static string ComputeHash(string priorHash, AuditEventEntity entity)
    {
        // Canonical, order-fixed representation — every field that identifies
        // the event content participates so tampering any one is detectable.
        var canonical = string.Join('|',
            priorHash,
            entity.Kind.ToString(),
            entity.OccurredAtUtc.ToUnixTimeMilliseconds().ToString(),
            entity.ActorUserId?.ToString() ?? "-",
            entity.ProjectId?.ToString() ?? "-",
            entity.DetailJson);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(bytes);
    }

    private static AuditEvent ToDomain(AuditEventEntity entity) => new()
    {
        Sequence = entity.Sequence,
        Kind = (AuditEventKind)entity.Kind,
        OccurredAtUtc = entity.OccurredAtUtc,
        ActorUserId = entity.ActorUserId,
        ProjectId = entity.ProjectId,
        DetailJson = entity.DetailJson,
        PriorHash = entity.PriorHash,
        RecordHash = entity.RecordHash,
    };
}
