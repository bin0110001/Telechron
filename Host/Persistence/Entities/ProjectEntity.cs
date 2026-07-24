namespace Telechron.Host.Persistence.Entities;

public sealed class ProjectEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public int RepairPolicy { get; set; }
    public Guid? ToolchainId { get; set; }
    public Guid? LlmConnectionId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public UserEntity? Owner { get; set; }
    public ToolchainEntity? Toolchain { get; set; }
    public LlmConnectionEntity? LlmConnection { get; set; }
    public List<ProjectMembershipEntity> Memberships { get; set; } = [];
    public List<SecretEntity> Secrets { get; set; } = [];
}
