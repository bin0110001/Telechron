namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for RequirementRevision (R-DM16b) — append-only.
public sealed class RequirementRevisionEntity
{
    public Guid Id { get; set; }
    public Guid RequirementId { get; set; }
    public int RevisionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int Status { get; set; }
    public Guid ChangedByUserId { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }

    public RequirementEntity? Requirement { get; set; }
    public UserEntity? ChangedByUser { get; set; }
}
