using Microsoft.Extensions.DependencyInjection;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Security.Tests.Fixtures;

// Secret.ProjectId (R-DM12) and ProjectMembership.ProjectId (R-DM15) are real
// FKs — tests that create Secrets/memberships need a genuine owning
// User+Project rather than a dangling random Guid.
public static class ProjectSeeding
{
    public static async Task<Guid> SeedProjectAsync(this IServiceScope scope)
    {
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();

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
