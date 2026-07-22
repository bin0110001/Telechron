using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IPersonaRepository : IRepository<Persona, Guid>
{
    Task<IReadOnlyList<Persona>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
}
