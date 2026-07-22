using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class ProjectMembershipRepository(TelechronDbContext db) : IProjectMembershipRepository
{
    public async Task<ProjectMembership?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.ProjectMemberships.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<ProjectMembership?> GetAsync(Guid userId, Guid projectId, CancellationToken ct = default)
    {
        var entity = await db.ProjectMemberships.AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.ProjectId == projectId, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<ProjectMembership>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        await db.ProjectMemberships.AsNoTracking().Where(m => m.UserId == userId).Select(m => m.ToDomain()).ToListAsync(ct);

    public async Task<IReadOnlyList<ProjectMembership>> GetAllAsync(CancellationToken ct = default) =>
        await db.ProjectMemberships.AsNoTracking().Select(m => m.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(ProjectMembership entity, CancellationToken ct = default)
    {
        await db.ProjectMemberships.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ProjectMembership entity, CancellationToken ct = default)
    {
        var existing = await db.ProjectMemberships.FirstOrDefaultAsync(m => m.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"ProjectMembership {entity.Id} not found.");
        existing.Role = (int)entity.Role;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.ProjectMemberships.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (existing is null) return;
        db.ProjectMemberships.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
