using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IProjectRepository : IRepository<Project, Guid>
{
    Task<IReadOnlyList<Project>> GetByOwnerAsync(Guid ownerUserId, CancellationToken ct = default);
}
