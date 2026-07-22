namespace Telechron.Host.Persistence.Entities;

public sealed class NotificationTargetEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    public UserEntity? User { get; set; }
}
