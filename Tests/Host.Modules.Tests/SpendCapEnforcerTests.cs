using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telechron.Host.Llm;
using Telechron.Host.Modules.Tests.Fixtures;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Modules.Tests;

// R-LLM4 against a real (file-backed SQLite) ILlmCallRepository -- proves
// the rolling-window sum and the global-vs-per-project independence, not
// just that the arithmetic on an in-memory list is correct.
public sealed class SpendCapEnforcerTests : IAsyncLifetime
{
    private LlmDispatcherTestFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new LlmDispatcherTestFixture(configureSpendCaps: o =>
        {
            o.WindowDuration = TimeSpan.FromHours(24);
            o.GlobalCapUsd = 10m;
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private static async Task RecordCallAsync(IServiceProvider services, decimal cost, Guid? projectId, DateTimeOffset? occurredAt = null)
    {
        var connectionRepo = services.GetRequiredService<ILlmConnectionRepository>();
        var connection = new LlmConnection
        {
            Id = Guid.NewGuid(), Name = "test-connection", Provider = "test",
            ConfigurationJson = "{}", SecretHandle = null, CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await connectionRepo.AddAsync(connection);

        var callRepo = services.GetRequiredService<ILlmCallRepository>();
        await callRepo.AddAsync(new LlmCall
        {
            Id = Guid.NewGuid(), LlmConnectionId = connection.Id, ProjectId = projectId,
            Provider = "test", Model = "test-model", PromptTokens = 100, CompletionTokens = 100,
            EstimatedCostUsd = cost, Succeeded = true, OccurredAtUtc = occurredAt ?? DateTimeOffset.UtcNow,
        });
    }

    [Fact]
    public async Task CheckAsync_NoCallsYet_IsAllowed()
    {
        using var scope = _fixture.CreateScope();
        var enforcer = scope.ServiceProvider.GetRequiredService<ISpendCapEnforcer>();

        var result = await enforcer.CheckAsync(projectId: null);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task CheckAsync_GlobalSpendUnderCap_IsAllowed()
    {
        using var scope = _fixture.CreateScope();
        await RecordCallAsync(scope.ServiceProvider, 5m, projectId: null);

        var enforcer = scope.ServiceProvider.GetRequiredService<ISpendCapEnforcer>();
        var result = await enforcer.CheckAsync(projectId: null);

        Assert.True(result.IsAllowed);
        Assert.Equal(5m, result.CurrentSpendUsd);
    }

    [Fact]
    public async Task CheckAsync_GlobalSpendAtOrOverCap_IsDenied()
    {
        using var scope = _fixture.CreateScope();
        await RecordCallAsync(scope.ServiceProvider, 6m, projectId: null);
        await RecordCallAsync(scope.ServiceProvider, 5m, projectId: null); // total 11 >= 10 cap

        var enforcer = scope.ServiceProvider.GetRequiredService<ISpendCapEnforcer>();
        var result = await enforcer.CheckAsync(projectId: null);

        Assert.False(result.IsAllowed);
        Assert.Contains("Global spend cap exceeded", result.Reason);
    }

    [Fact]
    public async Task CheckAsync_CallsOutsideWindow_AreExcludedFromSpend()
    {
        using var scope = _fixture.CreateScope();
        // Well outside the 24h window -- must not count toward the cap.
        await RecordCallAsync(scope.ServiceProvider, 100m, projectId: null, occurredAt: DateTimeOffset.UtcNow.AddDays(-3));

        var enforcer = scope.ServiceProvider.GetRequiredService<ISpendCapEnforcer>();
        var result = await enforcer.CheckAsync(projectId: null);

        Assert.True(result.IsAllowed);
        Assert.Equal(0m, result.CurrentSpendUsd);
    }

    [Fact]
    public async Task CheckAsync_PerProjectCapIndependentOfGlobalCap_IsEnforced()
    {
        // Project.Id must be seeded for real -- LlmCall.ProjectId is FK-
        // constrained -- so the Project is created before the per-project
        // cap (keyed by that same Id) can even be configured.
        using (var seedScope = _fixture.CreateScope())
        {
            var users = seedScope.ServiceProvider.GetRequiredService<Telechron.Sdk.Persistence.IUserRepository>();
            var owner = new User
            {
                Id = Guid.NewGuid(), DisplayName = "Test Owner", Email = $"{Guid.NewGuid():N}@telechron.dev",
                AuthCredentialHash = "hash", Role = Role.Admin, CreatedAtUtc = DateTimeOffset.UtcNow,
            };
            await users.AddAsync(owner);

            var projects = seedScope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var project = new Project
            {
                Id = Guid.NewGuid(), Name = "Test Project", RootPath = "/repo", OwnerUserId = owner.Id,
                RepairPolicy = RepairPolicy.RequireApproval, CreatedAtUtc = DateTimeOffset.UtcNow,
            };
            await projects.AddAsync(project);

            var projectId = project.Id;

            // Reconfigure with a tight per-project cap for this specific test.
            // Both fixtures point at the same underlying persistence, since
            // LlmDispatcherTestFixture creates its own isolated DB per
            // instance -- reuse THIS fixture's scope for the rest of the
            // test rather than a second fixture, so the seeded Project is
            // visible to the spend-cap check.
            var fixture2Options = new Dictionary<Guid, decimal> { [projectId] = 2m };

            using var scope = _fixture.CreateScope();
            await RecordCallAsync(scope.ServiceProvider, 3m, projectId); // over the $2 project cap

            var enforcer = new SpendCapEnforcer(
                scope.ServiceProvider.GetRequiredService<ILlmCallRepository>(),
                Options.Create(new SpendCapOptions { GlobalCapUsd = 1000m, PerProjectCapsUsd = fixture2Options }));

            var result = await enforcer.CheckAsync(projectId);

            Assert.False(result.IsAllowed);
            Assert.Contains("Project spend cap exceeded", result.Reason);
        }
    }
}
