using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Controllers;
using Telechron.Host.Security.Auth;
using Telechron.Host.Security.Tests.Fixtures;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Workflows.Approvals;

namespace Telechron.Host.Security.Tests.Auth;

// R-UI2: exercises the real Phase 10 Projects/Runs/Approvals/DesignDocument
// controllers end-to-end against the real Host (Program.cs), same
// TelechronApiFactory pattern as HumanApiAuthTests -- proves these are
// genuinely wired REST endpoints with real RBAC, not just classes that
// compile.
public sealed class FrontendApiControllersTests : IClassFixture<TelechronApiFactory>
{
    private readonly TelechronApiFactory _factory;

    public FrontendApiControllersTests(TelechronApiFactory factory) => _factory = factory;

    private async Task<(User user, string password, Guid projectId)> SeedUserWithOwnedProjectAsync(Role role)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var hashing = scope.ServiceProvider.GetRequiredService<PasswordHashing>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test User",
            Email = $"{Guid.NewGuid():N}@telechron.dev",
            AuthCredentialHash = string.Empty,
            Role = role,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        const string password = "correct horse battery staple 42!";
        user = user with { AuthCredentialHash = hashing.Hash(user, password) };
        await users.AddAsync(user);

        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Owned Test Project",
            RootPath = "/repo/owned",
            OwnerUserId = user.Id,
            RepairPolicy = RepairPolicy.RequireApproval,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await projects.AddAsync(project);

        return (user, password, project.Id);
    }

    private async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }

    private HttpClient AuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Projects_ListAsync_ReturnsOnlyProjectsVisibleToCaller()
    {
        var (user, password, ownedProjectId) = await SeedUserWithOwnedProjectAsync(Role.Viewer);
        using var scope = _factory.Services.CreateScope();
        var otherProjectId = await scope.SeedProjectAsync(); // owned by a different, unrelated User

        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));
        var response = await client.GetAsync("/api/projects");

        response.EnsureSuccessStatusCode();
        var projects = await response.Content.ReadFromJsonAsync<List<ProjectResponse>>();

        Assert.Contains(projects!, p => p.Id == ownedProjectId);
        Assert.DoesNotContain(projects!, p => p.Id == otherProjectId);
    }

    [Fact]
    public async Task Projects_GetAsync_ProjectNotOwnedByCaller_ReturnsForbidden()
    {
        var (user, password, _) = await SeedUserWithOwnedProjectAsync(Role.Viewer);
        using var scope = _factory.Services.CreateScope();
        var otherProjectId = await scope.SeedProjectAsync();

        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));
        var response = await client.GetAsync($"/api/projects/{otherProjectId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Runs_ListAsync_ForOwnedProject_ReturnsSeededRun()
    {
        var (user, password, projectId) = await SeedUserWithOwnedProjectAsync(Role.Viewer);
        using (var scope = _factory.Services.CreateScope())
        {
            var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
            await runs.AddAsync(new Run { Id = Guid.NewGuid(), ProjectId = projectId, Status = RunStatus.Passed });
        }

        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));
        var response = await client.GetAsync($"/api/runs?projectId={projectId}");

        response.EnsureSuccessStatusCode();
        var runsResult = await response.Content.ReadFromJsonAsync<List<RunResponse>>();
        Assert.Single(runsResult!);
        Assert.Equal(projectId, runsResult![0].ProjectId);
    }

    [Fact]
    public async Task Runs_ListAsync_ForProjectNotOwnedByCaller_ReturnsForbidden()
    {
        var (user, password, _) = await SeedUserWithOwnedProjectAsync(Role.Viewer);
        using var scope = _factory.Services.CreateScope();
        var otherProjectId = await scope.SeedProjectAsync();

        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));
        var response = await client.GetAsync($"/api/runs?projectId={otherProjectId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approvals_SubmitDecision_ApprovesRealPendingRequest()
    {
        var (user, password, _) = await SeedUserWithOwnedProjectAsync(Role.Operator);
        var approvalManager = _factory.Services.GetRequiredService<IApprovalManager>();
        var request = await approvalManager.CreateRequestAsync(Guid.NewGuid(), "step-1", "R-BUILD5-human-gate", "Approve?");

        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));

        var pendingResponse = await client.GetAsync("/api/approvals/pending");
        pendingResponse.EnsureSuccessStatusCode();
        var pending = await pendingResponse.Content.ReadFromJsonAsync<List<ApprovalResponse>>();
        Assert.Contains(pending!, a => a.Id == request.Id);

        var decisionResponse = await client.PostAsJsonAsync(
            $"/api/approvals/{request.Id}/decision", new SubmitApprovalDecisionRequest(true, "Looks good", null));

        decisionResponse.EnsureSuccessStatusCode();
        var updated = await decisionResponse.Content.ReadFromJsonAsync<ApprovalResponse>();
        Assert.True(updated!.IsSatisfied);
        Assert.Equal(user.Id, updated.ApprovedByUserId);
    }

    [Fact]
    public async Task Approvals_SubmitDecision_Unauthenticated_IsRejected()
    {
        var approvalManager = _factory.Services.GetRequiredService<IApprovalManager>();
        var request = await approvalManager.CreateRequestAsync(Guid.NewGuid(), "step-1", "gate", "Approve?");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/approvals/{request.Id}/decision", new SubmitApprovalDecisionRequest(true, null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DesignDocument_GetAsync_ForOwnedProjectWithNoDoc_ReturnsNotFound()
    {
        var (user, password, projectId) = await SeedUserWithOwnedProjectAsync(Role.Viewer);

        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));
        var response = await client.GetAsync($"/api/projects/{projectId}/design-document");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DesignDocument_GetAsync_ReturnsSeededRequirements()
    {
        var (user, password, projectId) = await SeedUserWithOwnedProjectAsync(Role.Viewer);
        Guid designDocId;
        using (var scope = _factory.Services.CreateScope())
        {
            var designDocs = scope.ServiceProvider.GetRequiredService<IDesignDocumentRepository>();
            var designDoc = new DesignDocument { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Test Design Doc", CreatedAtUtc = DateTimeOffset.UtcNow };
            await designDocs.AddAsync(designDoc);
            designDocId = designDoc.Id;

            var requirements = scope.ServiceProvider.GetRequiredService<IRequirementRepository>();
            await requirements.AddAsync(new Requirement
            {
                Id = Guid.NewGuid(), DesignDocumentId = designDocId, RequirementId = "R-TEST1",
                Title = "Test Requirement", Body = "A test requirement body.", Status = RequirementStatus.Active,
                CurrentRevisionNumber = 1, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));
        var response = await client.GetAsync($"/api/projects/{projectId}/design-document");

        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<DesignDocumentResponse>();
        Assert.Single(doc!.Requirements);
        Assert.Equal("R-TEST1", doc.Requirements[0].RequirementId);
    }
}
