using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Entities;
using Telechron.Host.Persistence.Mapping;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Repositories;

public sealed class UserRepository(TelechronDbContext db) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var entity = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) =>
        await db.Users.AsNoTracking().Select(u => u.ToDomain()).ToListAsync(ct);

    public async Task AddAsync(User entity, CancellationToken ct = default)
    {
        await db.Users.AddAsync(entity.ToEntity(), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User entity, CancellationToken ct = default)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Id == entity.Id, ct)
            ?? throw new InvalidOperationException($"User {entity.Id} not found.");
        entity.ApplyTo(existing);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (existing is null) return;
        db.Users.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
