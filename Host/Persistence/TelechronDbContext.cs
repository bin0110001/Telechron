using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Entities;

namespace Telechron.Host.Persistence;

public sealed class TelechronDbContext(DbContextOptions<TelechronDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<NotificationTargetEntity> NotificationTargets => Set<NotificationTargetEntity>();
    public DbSet<ProjectMembershipEntity> ProjectMemberships => Set<ProjectMembershipEntity>();
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<SecretEntity> Secrets => Set<SecretEntity>();
    public DbSet<MachineEntity> Machines => Set<MachineEntity>();
    public DbSet<LlmConnectionEntity> LlmConnections => Set<LlmConnectionEntity>();
    public DbSet<ToolchainEntity> Toolchains => Set<ToolchainEntity>();
    public DbSet<FunctionEntity> Functions => Set<FunctionEntity>();
    public DbSet<ModuleEntity> Modules => Set<ModuleEntity>();
    public DbSet<ConnectorEntity> Connectors => Set<ConnectorEntity>();
    public DbSet<ResourceEntity> Resources => Set<ResourceEntity>();
    public DbSet<RunEntity> Runs => Set<RunEntity>();
    public DbSet<PersonaEntity> Personas => Set<PersonaEntity>();
    public DbSet<WorkflowEntity> Workflows => Set<WorkflowEntity>();
    public DbSet<WorkflowRunEntity> WorkflowRuns => Set<WorkflowRunEntity>();
    public DbSet<FindingEntity> Findings => Set<FindingEntity>();
    public DbSet<IntentPlanEntity> IntentPlans => Set<IntentPlanEntity>();
    public DbSet<ArtifactEntity> Artifacts => Set<ArtifactEntity>();
    public DbSet<RepairAttemptEntity> RepairAttempts => Set<RepairAttemptEntity>();
    public DbSet<RepairAttemptFindingEntity> RepairAttemptFindings => Set<RepairAttemptFindingEntity>();
    public DbSet<DesignDocumentEntity> DesignDocuments => Set<DesignDocumentEntity>();
    public DbSet<RequirementEntity> Requirements => Set<RequirementEntity>();
    public DbSet<RequirementRevisionEntity> RequirementRevisions => Set<RequirementRevisionEntity>();
    public DbSet<AgentSessionEntity> AgentSessions => Set<AgentSessionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.DisplayName).IsRequired();
            e.Property(u => u.Email).IsRequired();
            e.Property(u => u.AuthCredentialHash).IsRequired();
        });

        modelBuilder.Entity<NotificationTargetEntity>(e =>
        {
            e.HasKey(n => n.Id);
            e.HasOne(n => n.User)
                .WithMany(u => u.NotificationTargets)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectMembershipEntity>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.UserId, m.ProjectId }).IsUnique();
            e.HasOne(m => m.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Project)
                .WithMany(p => p.Memberships)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectEntity>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired();
            e.Property(p => p.RootPath).IsRequired();
            e.HasOne(p => p.Owner)
                .WithMany()
                .HasForeignKey(p => p.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SecretEntity>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.Handle).IsUnique();
            e.Property(s => s.EncryptedValue).IsRequired();
            e.Property(s => s.EncryptionKeyId).IsRequired();
            e.HasOne(s => s.Project)
                .WithMany(p => p.Secrets)
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MachineEntity>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Name).IsRequired();
            e.Property(m => m.Hostname).IsRequired();
            e.Property(m => m.MachineFingerprint).IsRequired();
            e.HasIndex(m => m.MachineFingerprint).IsUnique();
        });

        modelBuilder.Entity<LlmConnectionEntity>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired();
            e.Property(c => c.Provider).IsRequired();
            e.Property(c => c.ConfigurationJson).IsRequired();
        });

        // ModuleId is a plain Guid, not a navigation FK, until Module lands —
        // see FunctionEntity/ToolchainEntity XML comments for why.
        modelBuilder.Entity<ToolchainEntity>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired();
            e.Property(t => t.BuildCommand).IsRequired();
            e.Property(t => t.TestCommand).IsRequired();
            e.Property(t => t.VerifyCommand).IsRequired();
        });

        modelBuilder.Entity<FunctionEntity>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Name).IsRequired();
            e.Property(f => f.Kind).IsRequired();
        });

        modelBuilder.Entity<ModuleEntity>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.Name);
            e.Property(m => m.Name).IsRequired();
            e.Property(m => m.Kind).IsRequired();
        });

        // ProjectId is optional (Connectors are reusable across projects,
        // R-DM11) — SetNull rather than Cascade so deleting a scoping Project
        // demotes the Connector to global instead of force-deleting it.
        modelBuilder.Entity<ConnectorEntity>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired();
            e.Property(c => c.Kind).IsRequired();
            e.HasOne(c => c.Project)
                .WithMany()
                .HasForeignKey(c => c.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ResourceEntity>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Kind).IsRequired();
            e.Property(r => r.Name).IsRequired();
            e.HasIndex(r => r.ExclusiveGroup);
            e.HasOne(r => r.Machine)
                .WithMany()
                .HasForeignKey(r => r.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RunEntity>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasOne(r => r.Project)
                .WithMany()
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Machine)
                .WithMany()
                .HasForeignKey(r => r.MachineId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PersonaEntity>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired();
            e.HasOne(p => p.Project)
                .WithMany()
                .HasForeignKey(p => p.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.LlmConnection)
                .WithMany()
                .HasForeignKey(p => p.LlmConnectionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkflowEntity>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.Name).IsRequired();
            e.HasOne(w => w.Project)
                .WithMany()
                .HasForeignKey(w => w.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkflowRunEntity>(e =>
        {
            e.HasKey(wr => wr.Id);
            e.HasOne(wr => wr.Workflow)
                .WithMany()
                .HasForeignKey(wr => wr.WorkflowId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FindingEntity>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.RootCauseSignature).IsRequired();
            e.HasOne(f => f.Project)
                .WithMany()
                .HasForeignKey(f => f.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Run)
                .WithMany()
                .HasForeignKey(f => f.RunId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<IntentPlanEntity>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasOne(p => p.Project)
                .WithMany()
                .HasForeignKey(p => p.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ArtifactEntity>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Name).IsRequired();
            e.Property(a => a.BlobRef).IsRequired();
            e.HasOne(a => a.WorkflowRun)
                .WithMany()
                .HasForeignKey(a => a.WorkflowRunId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RepairAttemptEntity>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.SnapshotRef).IsRequired();
            e.Property(r => r.PatchDiff).IsRequired();
            e.HasOne(r => r.Approver)
                .WithMany()
                .HasForeignKey(r => r.ApproverUserId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.ResultingArtifact)
                .WithMany()
                .HasForeignKey(r => r.ResultingArtifactId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RepairAttemptFindingEntity>(e =>
        {
            e.HasKey(l => new { l.RepairAttemptId, l.FindingId });
            e.HasOne(l => l.RepairAttempt)
                .WithMany(r => r.FindingLinks)
                .HasForeignKey(l => l.RepairAttemptId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Finding)
                .WithMany()
                .HasForeignKey(l => l.FindingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DesignDocumentEntity>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.ProjectId).IsUnique();
            e.Property(d => d.Title).IsRequired();
            e.HasOne(d => d.Project)
                .WithMany()
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RequirementEntity>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.DesignDocumentId, r.RequirementId }).IsUnique();
            e.Property(r => r.RequirementId).IsRequired();
            e.Property(r => r.Title).IsRequired();
            e.Property(r => r.Body).IsRequired();
            e.HasOne(r => r.DesignDocument)
                .WithMany()
                .HasForeignKey(r => r.DesignDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Append-only per R-DM16b — see RequirementRevisionRepository, which
        // is the sole write path and refuses Update/Delete.
        modelBuilder.Entity<RequirementRevisionEntity>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.RequirementId, r.RevisionNumber }).IsUnique();
            e.Property(r => r.Title).IsRequired();
            e.Property(r => r.Body).IsRequired();
            e.Property(r => r.ChangeReason).IsRequired();
            e.HasOne(r => r.Requirement)
                .WithMany()
                .HasForeignKey(r => r.RequirementId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.ChangedByUser)
                .WithMany()
                .HasForeignKey(r => r.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgentSessionEntity>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.SessionTokenHash).IsUnique();
            e.Property(s => s.SessionTokenHash).IsRequired();
            e.HasOne(s => s.Machine)
                .WithMany()
                .HasForeignKey(s => s.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
