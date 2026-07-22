namespace Telechron.Host.Persistence.Entities;

public sealed class SecretEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Handle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public byte[] EncryptedValue { get; set; } = [];
    public string EncryptionKeyId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }

    public ProjectEntity? Project { get; set; }
}
