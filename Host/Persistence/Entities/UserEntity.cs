namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for User (R-PER4 — kept distinct from the
// Telechron.Sdk.Domain.User POCO; TelechronDbContext maps between them).
public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AuthCredentialHash { get; set; } = string.Empty;
    public int Role { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public List<NotificationTargetEntity> NotificationTargets { get; set; } = [];
    public List<ProjectMembershipEntity> Memberships { get; set; } = [];
}
