namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for DesignDocument (R-DM16).
public sealed class DesignDocumentEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }

    public ProjectEntity? Project { get; set; }
}
