namespace Telechron.Sdk.Domain;

// A User's Role within a specific Project (R-DM15, R-DM1). Project ownership
// (R-DM1) is a distinct, single-User FK on Project — membership is the
// broader many-to-many RBAC grant.
public sealed class ProjectMembership
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required Guid ProjectId { get; init; }
    public required Role Role { get; init; }
}
