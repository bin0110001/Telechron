using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IModuleRepository : IRepository<Module, Guid>
{
    Task<Module?> GetByNameAsync(string name, CancellationToken ct = default);
}
