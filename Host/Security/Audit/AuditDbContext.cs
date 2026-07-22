using Microsoft.EntityFrameworkCore;

namespace Telechron.Host.Security.Audit;

// R-SEC7: a physically separate SQLite database from TelechronDbContext
// (R-PER1's operational store) — an attacker reaching the operational DB does
// not automatically reach the audit trail. No SaveChanges path here ever
// issues UPDATE/DELETE against AuditEvents; only inserts via AuditLog.Append.
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEventEntity>(e =>
        {
            e.HasKey(a => a.Sequence);
            e.Property(a => a.Sequence).ValueGeneratedOnAdd();
            e.Property(a => a.DetailJson).IsRequired();
            e.Property(a => a.PriorHash).IsRequired();
            e.Property(a => a.RecordHash).IsRequired();
            e.HasIndex(a => a.OccurredAtUtc);
            e.HasIndex(a => a.ProjectId);
        });
    }
}
