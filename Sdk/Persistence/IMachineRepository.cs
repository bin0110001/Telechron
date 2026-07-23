using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IMachineRepository : IRepository<Machine, Guid>
{
    Task<IReadOnlyList<Machine>> GetOnlineAsync(CancellationToken ct = default);

    // R-SCH3: registration dedup key.
    Task<Machine?> GetByFingerprintAsync(string machineFingerprint, CancellationToken ct = default);
}
