using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IUserRepository : IRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
}
