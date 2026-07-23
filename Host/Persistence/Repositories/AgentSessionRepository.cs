using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class AgentSessionRepository(TelechronDbContext db) : IAgentSessionRepository
{
    public async Task<AgentSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.AgentSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<AgentSession?> GetByTokenHashAsync(string sessionTokenHash, CancellationToken ct = default)
    {
        var entity = await db.AgentSessions.AsNoTracking().FirstOrDefaultAsync(s => s.SessionTokenHash == sessionTokenHash, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<AgentSession>> GetByMachineAsync(Guid machineId, CancellationToken ct = default) =>
        await db.AgentSessions.AsNoTracking().Where(s => s.MachineId == machineId).Select(s => s.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<AgentSession>> GetAllAsync(CancellationToken ct = default) =>
        await db.AgentSessions.AsNoTracking().Select(s => s.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(AgentSession entity, CancellationToken ct = default)
    {
        await db.AgentSessions.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AgentSession entity, CancellationToken ct = default)
    {
        var existing = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"AgentSession {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (existing is null) return;
        db.AgentSessions.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
