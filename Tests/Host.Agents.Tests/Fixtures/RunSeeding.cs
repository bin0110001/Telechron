using Microsoft.Extensions.DependencyInjection;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Agents.Tests.Fixtures;

// Run.ProjectId is a real FK -- these tests need a seeded Project (and its
// owning User) before a Run row can be inserted.
public static class RunSeeding
{
    public static async Task<Guid> SeedProjectAsync(this IServiceScope scope)
    {
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var owner = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test Owner",
            Email = $"{Guid.NewGuid():N}@telechron.dev",
            AuthCredentialHash = "hash:placeholder",
            Role = Role.Admin,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await users.AddAsync(owner);

        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            RootPath = "/repo/test",
            OwnerUserId = owner.Id,
            RepairPolicy = RepairPolicy.RequireApproval,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await projects.AddAsync(project);

        return project.Id;
    }
}
