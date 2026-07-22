namespace Telechron.Host.Security.Audit;

public sealed class AuditEventEntity
{
    public long Sequence { get; set; }
    public int Kind { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? ProjectId { get; set; }
    public string DetailJson { get; set; } = string.Empty;
    public string PriorHash { get; set; } = string.Empty;
    public string RecordHash { get; set; } = string.Empty;
}
