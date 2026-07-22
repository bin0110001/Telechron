using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IMachineRepository : IRepository<Machine, Guid>
{
    Task<IReadOnlyList<Machine>> GetOnlineAsync(CancellationToken ct = default);
}
