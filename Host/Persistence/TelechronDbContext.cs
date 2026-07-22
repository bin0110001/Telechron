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
    }
}
