using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IDesignDocumentRepository : IRepository<DesignDocument, Guid>
{
    Task<DesignDocument?> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
}
