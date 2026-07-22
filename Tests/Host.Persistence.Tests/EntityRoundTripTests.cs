using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Tests;

public sealed class EntityRoundTripTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task User_RoundTrips_ThroughRepository()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Ada Lovelace",
            Email = "ada@telechron.dev",
            AuthCredentialHash = "hash:placeholder",
            Role = Role.Admin,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            await repo.AddAsync(user);
        }

        using (var scope = _db.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var loaded = await repo.GetByIdAsync(user.Id);

            Assert.NotNull(loaded);
            Assert.Equal(user.DisplayName, loaded.DisplayName);
            Assert.Equal(user.Email, loaded.Email);
            Assert.Equal(user.Role, loaded.Role);

            var byEmail = await repo.GetByEmailAsync(user.Email);
            Assert.NotNull(byEmail);
            Assert.Equal(user.Id, byEmail.Id);
        }
    }

    [Fact]
    public async Task Project_RoundTrips_WithOwnerAndRepairPolicy()
    {
        var owner = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Grace Hopper",
            Email = "grace@telechron.dev",
            AuthCredentialHash = "hash:placeholder",
            Role = Role.Operator,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Telechron Core",
            RootPath = "/repo/telechron",
            OwnerUserId = owner.Id,
            RepairPolicy = RepairPolicy.RequireApproval,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(owner);
            await scope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
        }

        using (var scope = _db.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var loaded = await repo.GetByIdAsync(project.Id);

            Assert.NotNull(loaded);
            Assert.Equal(project.Name, loaded.Name);
            Assert.Equal(project.OwnerUserId, loaded.OwnerUserId);
            Assert.Equal(RepairPolicy.RequireApproval, loaded.RepairPolicy);

            var byOwner = await repo.GetByOwnerAsync(owner.Id);
            Assert.Single(byOwner);
        }
    }

    [Fact]
    public async Task Secret_RoundTrips_WithEncryptedValueAndHandle()
    {
        var owner = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Owner",
            Email = "owner@telechron.dev",
            AuthCredentialHash = "hash:placeholder",
            Role = Role.Admin,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Secretful Project",
            RootPath = "/repo/secretful",
            OwnerUserId = owner.Id,
            RepairPolicy = RepairPolicy.FullyAutonomous,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        var secret = new Secret
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Handle = "secret://telechron/github-pat",
            Name = "GitHub PAT",
            EncryptedValue = [0x01, 0x02, 0x03, 0x04],
            EncryptionKeyId = "kek-placeholder-v1",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(owner);
            await scope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
            await scope.ServiceProvider.GetRequiredService<ISecretRepository>().AddAsync(secret);
        }

        using (var scope = _db.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISecretRepository>();
            var loaded = await repo.GetByHandleAsync(secret.Handle);

            Assert.NotNull(loaded);
            Assert.Equal(secret.EncryptedValue, loaded.EncryptedValue);
            Assert.Equal(secret.EncryptionKeyId, loaded.EncryptionKeyId);
            Assert.Null(loaded.RevokedAtUtc);

            var byProject = await repo.GetByProjectAsync(project.Id);
            Assert.Single(byProject);
        }
    }
}
