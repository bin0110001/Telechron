using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Controllers;
using Telechron.Host.Persistence;
using Telechron.Host.Security.Auth;
using Telechron.Host.Security.Tests.Fixtures;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Security.Tests.Auth;

// R-SEC6 end-to-end: an unauthenticated request to a mutating endpoint is
// rejected; a Viewer cannot hit an Operator-gated endpoint; CORS blocks a
// non-allowlisted origin.
public sealed class HumanApiAuthTests : IClassFixture<TelechronApiFactory>
{
    private readonly TelechronApiFactory _factory;

    public HumanApiAuthTests(TelechronApiFactory factory) => _factory = factory;

    // ProjectMembership.ProjectId is a real FK — when a caller wants project
    // scoping, this seeds an actual owning Project (with a throwaway owner
    // distinct from the test User) rather than a dangling Guid, and returns
    // its ID for the caller to use in the request URL.
    private async Task<(User user, string password, Guid projectId)> SeedUserAsync(Role role, Role? projectRole = null)
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

        var projectId = await scope.SeedProjectAsync();

        if (projectRole is not null)
        {
            var memberships = scope.ServiceProvider.GetRequiredService<IProjectMembershipRepository>();
            await memberships.AddAsync(new ProjectMembership
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProjectId = projectId,
                Role = projectRole.Value,
            });
        }

        return (user, password, projectId);
    }

    private async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }

    [Fact]
    public async Task UnauthenticatedRequest_ToMutatingEndpoint_IsRejected()
    {
        var client = _factory.CreateClient();
        var projectId = Guid.NewGuid();

        var response = await client.PostAsync($"/api/diagnostics/{projectId}/operator-only", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_IssuesToken()
    {
        var (user, password, _) = await SeedUserAsync(Role.Viewer);
        var client = _factory.CreateClient();

        var token = await LoginAsync(client, user.Email, password);

        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var (user, _, _) = await SeedUserAsync(Role.Viewer);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_CannotApprove_OperatorGatedEndpoint()
    {
        var (user, password, projectId) = await SeedUserAsync(Role.Viewer, Role.Viewer);
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, user.Email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/diagnostics/{projectId}/operator-only", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CanAccess_OperatorGatedEndpoint_ForTheirProject()
    {
        var (user, password, projectId) = await SeedUserAsync(Role.Viewer, Role.Operator);
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, user.Email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/diagnostics/{projectId}/operator-only", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CannotAccess_AdminGatedEndpoint()
    {
        var (user, password, projectId) = await SeedUserAsync(Role.Viewer, Role.Operator);
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, user.Email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/diagnostics/{projectId}/admin-only", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GlobalAdmin_CanAccess_AnyProjectsGatedEndpoint()
    {
        var (user, password, _) = await SeedUserAsync(Role.Admin);
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, user.Email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/diagnostics/{Guid.NewGuid()}/admin-only", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cors_AllowsConfiguredOrigin()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/diagnostics/whoami");
        request.Headers.Add("Origin", TelechronApiFactory.AllowedOrigin);

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cors_BlocksNonAllowlistedOrigin()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/diagnostics/whoami");
        request.Headers.Add("Origin", "https://not-allowed.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
