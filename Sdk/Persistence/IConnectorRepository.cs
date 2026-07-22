using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IConnectorRepository : IRepository<Connector, Guid>
{
    // projectId: null returns globally-shared Connectors; a value scopes to
    // that Project's Connectors.
    Task<IReadOnlyList<Connector>> GetByProjectAsync(Guid? projectId, CancellationToken ct = default);
}
