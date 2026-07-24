using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Telechron.Host.Controllers;
using Telechron.Host.Security.Auth;
using Telechron.Host.Security.Tests.Fixtures;

namespace Telechron.Host.Security.Tests.Auth;

// R-SEC6: the one-time bootstrap path -- proves the endpoint only works
// with the right token, only while zero Users exist, and that a normal
// login works against the User it creates. Every test here constructs
// its OWN TelechronApiFactory (not the shared IClassFixture instance)
// because this controller's whole behavior hinges on "does even one User
// exist yet" -- sharing a DB across these tests would make each test's
// outcome depend on execution order.
public sealed class SetupControllerTests
{
    [Fact]
    public async Task GetStatus_FreshHost_ReportsSetupNotComplete()
    {
        await using var factory = new TelechronApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/setup/status");

        response.EnsureSuccessStatusCode();
        var status = await response.Content.ReadFromJsonAsync<SetupStatusResponse>();
        Assert.False(status!.IsSetupComplete);
    }

    [Fact]
    public async Task CreateFirstAdmin_WrongToken_ReturnsUnauthorized()
    {
        await using var factory = new TelechronApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/setup/first-admin", new CreateFirstAdminRequest(
            SetupToken: "not-the-real-token", Email: $"{Guid.NewGuid():N}@telechron.dev", Password: "correct horse battery staple", DisplayName: "Wrong Token Admin"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateFirstAdmin_TooShortPassword_ReturnsBadRequest()
    {
        await using var factory = new TelechronApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/setup/first-admin", new CreateFirstAdminRequest(
            SetupToken: TelechronApiFactory.SetupToken, Email: $"{Guid.NewGuid():N}@telechron.dev", Password: "short", DisplayName: "Short Password Admin"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateFirstAdmin_NoSetupTokenConfigured_ReturnsServiceUnavailable()
    {
        await using var factory = new TelechronApiFactory();
        // Same factory instance, but re-hosted with SetupToken cleared --
        // proves a fresh deploy that never opted in to bootstrap cannot be
        // bootstrapped at all, by design. This is still the ONLY web host
        // ever built/run for this factory's DataDirectory, so no cross-test
        // DB sharing concern applies.
        await using var factoryWithNoToken = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Telechron:SetupToken", ""));
        var client = factoryWithNoToken.CreateClient();

        var response = await client.PostAsJsonAsync("/api/setup/first-admin", new CreateFirstAdminRequest(
            SetupToken: TelechronApiFactory.SetupToken, Email: $"{Guid.NewGuid():N}@telechron.dev", Password: "correct horse battery staple", DisplayName: "No Token Admin"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task CreateFirstAdmin_CorrectToken_CreatesAdminAndAllowsLogin_ThenRefusesASecondBootstrap()
    {
        await using var factory = new TelechronApiFactory();
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@telechron.dev";
        const string password = "correct horse battery staple 42!";

        var createResponse = await client.PostAsJsonAsync("/api/setup/first-admin", new CreateFirstAdminRequest(
            SetupToken: TelechronApiFactory.SetupToken, Email: email, Password: password, DisplayName: "First Admin"));

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateFirstAdminResponse>();
        Assert.Equal(email, created!.Email);

        // The created User is a real, working Admin -- can log in through
        // the ordinary /api/auth/login path, no special-cased session.
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        loginResponse.EnsureSuccessStatusCode();
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrEmpty(loginBody!.AccessToken));

        // Setup status now reports complete.
        var statusResponse = await client.GetAsync("/api/setup/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<SetupStatusResponse>();
        Assert.True(status!.IsSetupComplete);

        // A second bootstrap attempt against the same (now non-empty) DB
        // is refused, even with the correct token -- the token alone is
        // not sufficient to mint additional privileged accounts.
        var secondAttempt = await client.PostAsJsonAsync("/api/setup/first-admin", new CreateFirstAdminRequest(
            SetupToken: TelechronApiFactory.SetupToken, Email: $"{Guid.NewGuid():N}@telechron.dev", Password: "another password entirely", DisplayName: "Second Admin"));
        Assert.Equal(HttpStatusCode.Conflict, secondAttempt.StatusCode);
    }

    // Regression test for a real bug caught only by live/browser smoke
    // testing, not by the other tests in this file: Program.cs runs
    // ReflexiveDesignDocumentSeeder (R-DM16a) on every startup, which
    // creates a non-loginable "Telechron System" User. The other tests
    // above never exercise that seeder because the WebApplicationFactory's
    // content root doesn't resolve to a directory containing TechDesign.md
    // -- so they passed even when GetStatusAsync/CreateFirstAdminAsync
    // used a naive "any User exists" check that the system user would
    // have permanently tripped on every real deployment. This test forces
    // the seeder to run against the actual repo's TechDesign.md via
    // RepoRootOverride, proving setup status still correctly reports
    // "not complete" with only the system user present.
    [Fact]
    public async Task GetStatus_WithOnlySeededSystemUser_StillReportsSetupNotComplete()
    {
        var repoRoot = FindRepoRoot();
        await using var factory = new TelechronApiFactory { RepoRootOverride = repoRoot };
        var client = factory.CreateClient();

        var statusResponse = await client.GetAsync("/api/setup/status");
        statusResponse.EnsureSuccessStatusCode();
        var status = await statusResponse.Content.ReadFromJsonAsync<SetupStatusResponse>();
        Assert.False(status!.IsSetupComplete);

        // And the bootstrap endpoint must still work despite the seeded
        // system user already existing in the Users table.
        var createResponse = await client.PostAsJsonAsync("/api/setup/first-admin", new CreateFirstAdminRequest(
            SetupToken: TelechronApiFactory.SetupToken, Email: $"{Guid.NewGuid():N}@telechron.dev", Password: "correct horse battery staple", DisplayName: "Real Admin"));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TechDesign.md")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root containing TechDesign.md from " + AppContext.BaseDirectory);
    }
}
