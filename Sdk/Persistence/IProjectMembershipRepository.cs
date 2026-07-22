using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IProjectMembershipRepository : IRepository<ProjectMembership, Guid>
{
    Task<IReadOnlyList<ProjectMembership>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<ProjectMembership?> GetAsync(Guid userId, Guid projectId, CancellationToken ct = default);
}
