using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IAgentSessionRepository : IRepository<AgentSession, Guid>
{
    Task<AgentSession?> GetByTokenHashAsync(string sessionTokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<AgentSession>> GetByMachineAsync(Guid machineId, CancellationToken ct = default);
}
