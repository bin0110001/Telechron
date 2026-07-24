using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Controllers;
using Telechron.Host.Security.Auth;
using Telechron.Host.Security.Tests.Fixtures;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Scheduling;

namespace Telechron.Host.Security.Tests.Auth;

// R-UI2: exercises the remaining Machines/Modules/Scheduling/Secrets/Llm/
// AuditLog/Provenance/PrivilegedDiff/Workflows controllers against the
// real Host, same TelechronApiFactory pattern as FrontendApiControllersTests.
public sealed class RemainingFrontendApiControllersTests : IClassFixture<TelechronApiFactory>
{
    private readonly TelechronApiFactory _factory;

    public RemainingFrontendApiControllersTests(TelechronApiFactory factory) => _factory = factory;

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
    public async Task Machines_ListAsync_ReturnsSeededMachine()
    {
        var (user, password, _) = await SeedUserWithOwnedProjectAsync(Role.Viewer);
        using (var scope = _factory.Services.CreateScope())
        {
            var machines = scope.ServiceProvider.GetRequiredService<IMachineRepository>();
            await machines.AddAsync(new Machine
            {
                Id = Guid.NewGuid(), Name = "Test Machine", Hostname = "test-host",
                MachineFingerprint = Guid.NewGuid().ToString("N"), RegisteredAtUtc = DateTimeOffset.UtcNow, IsOnline = true,
            });
        }

        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));
        var response = await client.GetAsync("/api/machines");

        response.EnsureSuccessStatusCode();
        var machinesResult = await response.Content.ReadFromJsonAsync<List<MachineResponse>>();
        Assert.Contains(machinesResult!, m => m.Name == "Test Machine");
    }

    [Fact]
    public async Task Modules_ListAsync_ReturnsSeededModule()
    {
        var (user, password, _) = await SeedUserWithOwnedProjectAsync(Role.Viewer);
        using (var scope = _factory.Services.CreateScope())
        {
            var modules = scope.ServiceProvider.GetRequiredService<IModuleRepository>();
            await modules.AddAsync(new Module
            {
                Id = Guid.NewGuid(), Name = "test.module", Kind = "function-executor",
                VersionMajor = 1, VersionMinor = 0, VersionPatch = 0,
                CapabilitiesJson = "[]", TestCommand = "test", SourceCodeRef = "n/a", InstalledAtUtc = DateTimeOffset.UtcNow,
            });
        }

        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));
        var response = await client.GetAsync("/api/modules");

        response.EnsureSuccessStatusCode();
        var modulesResult = await response.Content.ReadFromJsonAsync<List<ModuleResponse>>();
        Assert.Contains(modulesResult!, m => m.Name == "test.module");
    }

    [Fact]
    public async Task Scheduling_ListAsync_ForProjectNotOwnedByCaller_ReturnsForbidden()
    {
        var (user, password, _) = await SeedUserWithOwnedProjectAsync(Role.Viewer);
        using var scope = _factory.Services.CreateScope();
        var otherProjectId = await scope.SeedProjectAsync();

        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));
        var response = await client.GetAsync($"/api/projects/{otherProjectId}/schedules");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Secrets_ListAsync_NeverExposesEncryptedValue()
    {
        var (user, password, projectId) = await SeedUserWithOwnedProjectAsync(Role.Viewer);
        using (var scope = _factory.Services.CreateScope())
        {
            var secrets = scope.ServiceProvider.GetRequiredService<ISecretRepository>();
            await secrets.AddAsync(new Secret
            {
                Id = Guid.NewGuid(), ProjectId = projectId, Handle = "handle-123", Name = "TEST_SECRET",
                EncryptedValue = [1, 2, 3], EncryptionKeyId = "key-1", CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));
        var response = await client.GetAsync($"/api/projects/{projectId}/secrets");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("EncryptedValue", body);
        Assert.DoesNotContain("encryptedValue", body);
        Assert.Contains("handle-123", body);
    }

    [Fact]
    public async Task Llm_ListConnectionsAsync_ReturnsSeededConnection()
    {
        var (user, password, _) = await SeedUserWithOwnedProjectAsync(Role.Viewer);
        using (var scope = _factory.Services.CreateScope())
        {
            var connections = scope.ServiceProvider.GetRequiredService<ILlmConnectionRepository>();
            await connections.AddAsync(new LlmConnection
            {
                Id = Guid.NewGuid(), Name = "Test Connection", Provider = "test-provider",
                ConfigurationJson = "{}", CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));
        var response = await client.GetAsync("/api/llm/connections");

        response.EnsureSuccessStatusCode();
        var connectionsResult = await response.Content.ReadFromJsonAsync<List<LlmConnectionResponse>>();
        Assert.Contains(connectionsResult!, c => c.Name == "Test Connection");
    }

    [Fact]
    public async Task AuditLog_ListAsync_NonAdmin_ReturnsForbidden()
    {
        var (user, password, _) = await SeedUserWithOwnedProjectAsync(Role.Operator);
        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));

        var response = await client.GetAsync("/api/audit-log");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Merged into one test (both need an Admin login) -- the shared
    // TelechronApiFactory's auth rate limiter (10 logins/min/IP, all test
    // clients share the loopback IP) is a real, correct production
    // control; this test class stays under it by not spending a login per
    // assertion when one login can cover both.
    [Fact]
    public async Task AuditLog_GlobalAdmin_ListAndVerifyBothSucceed()
    {
        var (user, password, _) = await SeedUserWithOwnedProjectAsync(Role.Admin);
        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));

        var listResponse = await client.GetAsync("/api/audit-log");
        listResponse.EnsureSuccessStatusCode();

        var verifyResponse = await client.GetAsync("/api/audit-log/verify");
        verifyResponse.EnsureSuccessStatusCode();
        var result = await verifyResponse.Content.ReadFromJsonAsync<AuditChainVerificationResponse>();
        Assert.True(result!.IsIntact);
    }

    [Fact]
    public async Task Provenance_PendingRepairDiffs_And_Workflows_ForOwnedProjectWithNoData_AllReturnEmpty()
    {
        var (user, password, projectId) = await SeedUserWithOwnedProjectAsync(Role.Viewer);
        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));

        var attemptsResponse = await client.GetAsync($"/api/projects/{projectId}/repair-attempts");
        attemptsResponse.EnsureSuccessStatusCode();
        Assert.Empty((await attemptsResponse.Content.ReadFromJsonAsync<List<RepairAttemptResponse>>())!);

        var diffsResponse = await client.GetAsync($"/api/projects/{projectId}/pending-repair-diffs");
        diffsResponse.EnsureSuccessStatusCode();

        var workflowsResponse = await client.GetAsync($"/api/projects/{projectId}/workflows");
        workflowsResponse.EnsureSuccessStatusCode();
        Assert.Empty((await workflowsResponse.Content.ReadFromJsonAsync<List<WorkflowResponse>>())!);
    }

    [Fact]
    public async Task PendingRepairDiffs_ListAsync_ForProjectNotOwnedByCaller_ReturnsForbidden()
    {
        var (user, password, _) = await SeedUserWithOwnedProjectAsync(Role.Viewer);
        using var scope = _factory.Services.CreateScope();
        var otherProjectId = await scope.SeedProjectAsync();

        var client = AuthenticatedClient(await LoginAsync(_factory.CreateClient(), user.Email, password));
        var response = await client.GetAsync($"/api/projects/{otherProjectId}/pending-repair-diffs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

}
