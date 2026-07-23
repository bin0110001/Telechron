using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Telechron.Host.DesignDocuments;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Tests.DesignDocuments;

public sealed class ReflexiveDesignDocumentSeederTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private ReflexiveDesignDocumentSeeder CreateSeeder(IServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<IUserRepository>(),
        scope.ServiceProvider.GetRequiredService<IProjectRepository>(),
        scope.ServiceProvider.GetRequiredService<IDesignDocumentRepository>(),
        scope.ServiceProvider.GetRequiredService<IRequirementRepository>(),
        scope.ServiceProvider.GetRequiredService<IRequirementRevisionRepository>(),
        NullLogger<ReflexiveDesignDocumentSeeder>.Instance);

    private const string SampleMarkdown = """
        R-NS1 — Modularize Everything
        Every leaf capability lives in a module.

        R-NS2 — One Repair Loop
        There is exactly one repair pipeline.
        """;

    [Fact]
    public async Task SeedFromMarkdown_CreatesSystemProjectAndDesignDocument()
    {
        using (var scope = _db.CreateScope())
            await CreateSeeder(scope).SeedFromMarkdownAsync(SampleMarkdown, "/repo/telechron");

        using var verifyScope = _db.CreateScope();
        var projects = await verifyScope.ServiceProvider.GetRequiredService<IProjectRepository>().GetAllAsync();
        var systemProject = Assert.Single(projects, p => p.Name == ReflexiveDesignDocumentSeeder.SystemProjectName);

        var doc = await verifyScope.ServiceProvider.GetRequiredService<IDesignDocumentRepository>()
            .GetByProjectAsync(systemProject.Id);
        Assert.NotNull(doc);
    }

    [Fact]
    public async Task SeedFromMarkdown_CreatesOneRequirementPerParsedBlock()
    {
        using (var scope = _db.CreateScope())
            await CreateSeeder(scope).SeedFromMarkdownAsync(SampleMarkdown, "/repo/telechron");

        using var verifyScope = _db.CreateScope();
        var projects = await verifyScope.ServiceProvider.GetRequiredService<IProjectRepository>().GetAllAsync();
        var systemProject = projects.Single(p => p.Name == ReflexiveDesignDocumentSeeder.SystemProjectName);
        var doc = await verifyScope.ServiceProvider.GetRequiredService<IDesignDocumentRepository>().GetByProjectAsync(systemProject.Id);

        var requirements = await verifyScope.ServiceProvider.GetRequiredService<IRequirementRepository>()
            .GetByDesignDocumentAsync(doc!.Id);

        Assert.Equal(2, requirements.Count);
        Assert.Contains(requirements, r => r.RequirementId == "R-NS1" && r.Title == "Modularize Everything");
        Assert.Contains(requirements, r => r.RequirementId == "R-NS2");
    }

    [Fact]
    public async Task SeedFromMarkdown_IsIdempotent_SecondRunAddsNothing()
    {
        using (var scope = _db.CreateScope())
        {
            await CreateSeeder(scope).SeedFromMarkdownAsync(SampleMarkdown, "/repo/telechron");
            await CreateSeeder(scope).SeedFromMarkdownAsync(SampleMarkdown, "/repo/telechron");
        }

        using var verifyScope = _db.CreateScope();
        var projects = await verifyScope.ServiceProvider.GetRequiredService<IProjectRepository>().GetAllAsync();
        Assert.Single(projects, p => p.Name == ReflexiveDesignDocumentSeeder.SystemProjectName);

        var systemProject = projects.Single(p => p.Name == ReflexiveDesignDocumentSeeder.SystemProjectName);
        var doc = await verifyScope.ServiceProvider.GetRequiredService<IDesignDocumentRepository>().GetByProjectAsync(systemProject.Id);
        var requirements = await verifyScope.ServiceProvider.GetRequiredService<IRequirementRepository>().GetByDesignDocumentAsync(doc!.Id);

        Assert.Equal(2, requirements.Count); // not 4 — no duplicates from the second run
        Assert.All(requirements, r => Assert.Equal(1, r.CurrentRevisionNumber));
    }

    [Fact]
    public async Task SeedFromMarkdown_ChangedRequirementText_CreatesNewRevision_ButNotDuplicateRequirement()
    {
        using (var scope = _db.CreateScope())
            await CreateSeeder(scope).SeedFromMarkdownAsync(SampleMarkdown, "/repo/telechron");

        const string updatedMarkdown = """
            R-NS1 — Modularize Everything
            Every leaf capability lives in a hot-reloadable module (updated wording).

            R-NS2 — One Repair Loop
            There is exactly one repair pipeline.
            """;

        using (var scope = _db.CreateScope())
            await CreateSeeder(scope).SeedFromMarkdownAsync(updatedMarkdown, "/repo/telechron");

        using var verifyScope = _db.CreateScope();
        var projects = await verifyScope.ServiceProvider.GetRequiredService<IProjectRepository>().GetAllAsync();
        var systemProject = projects.Single(p => p.Name == ReflexiveDesignDocumentSeeder.SystemProjectName);
        var doc = await verifyScope.ServiceProvider.GetRequiredService<IDesignDocumentRepository>().GetByProjectAsync(systemProject.Id);

        var requirementRepo = verifyScope.ServiceProvider.GetRequiredService<IRequirementRepository>();
        var requirements = await requirementRepo.GetByDesignDocumentAsync(doc!.Id);
        Assert.Equal(2, requirements.Count); // still one Requirement row per R-XXX id, not a duplicate

        var ns1 = requirements.Single(r => r.RequirementId == "R-NS1");
        Assert.Contains("updated wording", ns1.Body, StringComparison.Ordinal);
        Assert.Equal(2, ns1.CurrentRevisionNumber);

        var revisions = await verifyScope.ServiceProvider.GetRequiredService<IRequirementRevisionRepository>()
            .GetByRequirementAsync(ns1.Id);
        Assert.Equal(2, revisions.Count); // R-DM16b: history preserved, not overwritten
        Assert.Contains(revisions, r => r.RevisionNumber == 1 && !r.Body.Contains("updated wording"));
        Assert.Contains(revisions, r => r.RevisionNumber == 2 && r.Body.Contains("updated wording"));
    }

    [Fact]
    public async Task SeedFromMarkdown_ReusesExistingSystemProject_AcrossSeparateSeederInstances()
    {
        Guid firstProjectId;
        using (var scope = _db.CreateScope())
        {
            await CreateSeeder(scope).SeedFromMarkdownAsync(SampleMarkdown, "/repo/telechron");
            var firstRunProjects = await scope.ServiceProvider.GetRequiredService<IProjectRepository>().GetAllAsync();
            firstProjectId = firstRunProjects.Single(p => p.Name == ReflexiveDesignDocumentSeeder.SystemProjectName).Id;
        }

        using (var scope = _db.CreateScope())
            await CreateSeeder(scope).SeedFromMarkdownAsync(SampleMarkdown, "/repo/telechron");

        using var verifyScope = _db.CreateScope();
        var projects = await verifyScope.ServiceProvider.GetRequiredService<IProjectRepository>().GetAllAsync();
        var systemProjects = projects.Where(p => p.Name == ReflexiveDesignDocumentSeeder.SystemProjectName).ToList();

        Assert.Single(systemProjects);
        Assert.Equal(firstProjectId, systemProjects[0].Id);
    }
}
