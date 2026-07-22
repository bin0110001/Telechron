namespace Telechron.Host.Persistence.Entities;

// Many-to-many join between RepairAttempt and Finding (R-DM3a: "Source
// Finding(s) — many-to-many"). Composite key on both FKs.
public sealed class RepairAttemptFindingEntity
{
    public Guid RepairAttemptId { get; set; }
    public Guid FindingId { get; set; }

    public RepairAttemptEntity? RepairAttempt { get; set; }
    public FindingEntity? Finding { get; set; }
}
