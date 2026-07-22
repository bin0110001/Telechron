using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IRequirementRepository : IRepository<Requirement, Guid>
{
    Task<IReadOnlyList<Requirement>> GetByDesignDocumentAsync(Guid designDocumentId, CancellationToken ct = default);
    Task<Requirement?> GetByRequirementIdAsync(Guid designDocumentId, string requirementId, CancellationToken ct = default);
}
