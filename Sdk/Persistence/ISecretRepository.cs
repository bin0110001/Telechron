using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface ISecretRepository : IRepository<Secret, Guid>
{
    Task<Secret?> GetByHandleAsync(string handle, CancellationToken ct = default);
    Task<IReadOnlyList<Secret>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
}
