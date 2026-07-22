namespace Telechron.Host.Persistence.Entities;

public sealed class ProjectMembershipEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public int Role { get; set; }

    public UserEntity? User { get; set; }
    public ProjectEntity? Project { get; set; }
}
