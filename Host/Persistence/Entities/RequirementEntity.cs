namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for Requirement (R-DM16) — current state; history
// lives in RequirementRevisionEntity.
public sealed class RequirementEntity
{
    public Guid Id { get; set; }
    public Guid DesignDocumentId { get; set; }
    public string RequirementId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int Status { get; set; }
    public int CurrentRevisionNumber { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DesignDocumentEntity? DesignDocument { get; set; }
}
