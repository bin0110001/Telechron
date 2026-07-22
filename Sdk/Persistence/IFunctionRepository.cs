using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IFunctionRepository : IRepository<Function, Guid>
{
    Task<IReadOnlyList<Function>> GetByModuleAsync(Guid moduleId, CancellationToken ct = default);
}
