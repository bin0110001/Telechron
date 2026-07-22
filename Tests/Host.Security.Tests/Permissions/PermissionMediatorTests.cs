using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Security.Audit;
using Telechron.Host.Security.Permissions;
using Telechron.Host.Security.Tests.Fixtures;
using Telechron.Sdk.Security.Audit;
using Telechron.Sdk.Security.Permissions;

namespace Telechron.Host.Security.Tests.Permissions;

public sealed class PermissionMediatorTests : IAsyncLifetime
{
    private SqliteAuditTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteAuditTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private PermissionMediator CreateMediator(IServiceScope scope) =>
        new(scope.ServiceProvider.GetRequiredService<IAuditLog>());

    [Fact]
    public async Task Authorize_DeniesByDefault_WhenAllowlistIsEmpty()
    {
        using var scope = _db.CreateScope();
        var mediator = CreateMediator(scope);
        var request = new CapabilityRequest(Guid.NewGuid(), CapabilityKind.SecretAccess, "sec_x", null);

        var result = await mediator.AuthorizeAsync(request, []);

        Assert.False(result.IsAuthorized);
    }

    [Fact]
    public async Task Authorize_Grants_WhenAllowlistHasMatchingResourceScopedGrant()
    {
        using var scope = _db.CreateScope();
        var mediator = CreateMediator(scope);
        var request = new CapabilityRequest(Guid.NewGuid(), CapabilityKind.SecretAccess, "sec_x", null);
        var allowlist = new[] { new CapabilityGrant(CapabilityKind.SecretAccess, "sec_x") };

        var result = await mediator.AuthorizeAsync(request, allowlist);

        Assert.True(result.IsAuthorized);
    }

    [Fact]
    public async Task Authorize_Denies_WhenResourceIdDiffersFromGrant()
    {
        using var scope = _db.CreateScope();
        var mediator = CreateMediator(scope);
        var request = new CapabilityRequest(Guid.NewGuid(), CapabilityKind.SecretAccess, "sec_other", null);
        var allowlist = new[] { new CapabilityGrant(CapabilityKind.SecretAccess, "sec_x") };

        var result = await mediator.AuthorizeAsync(request, allowlist);

        Assert.False(result.IsAuthorized);
    }

    [Fact]
    public async Task Authorize_Grants_WhenKindWideGrantPresent()
    {
        using var scope = _db.CreateScope();
        var mediator = CreateMediator(scope);
        var request = new CapabilityRequest(Guid.NewGuid(), CapabilityKind.LlmAccess, "any-model", null);
        var allowlist = new[] { new CapabilityGrant(CapabilityKind.LlmAccess, null) };

        var result = await mediator.AuthorizeAsync(request, allowlist);

        Assert.True(result.IsAuthorized);
    }

    [Fact]
    public async Task Authorize_IgnoresAllowlistEntriesOfADifferentKind()
    {
        using var scope = _db.CreateScope();
        var mediator = CreateMediator(scope);
        var request = new CapabilityRequest(Guid.NewGuid(), CapabilityKind.GpuAccess, null, null);
        var allowlist = new[] { new CapabilityGrant(CapabilityKind.FilesystemRead, null) };

        var result = await mediator.AuthorizeAsync(request, allowlist);

        Assert.False(result.IsAuthorized);
    }

    [Fact]
    public async Task Authorize_CannotBeInfluencedByRequestContentAlone_OnlyByAllowlist()
    {
        // R-MOD8a: the decision is a pure function of (request, allowlist) — a
        // request cannot "argue its way in" by carrying extra data. Two
        // requests differing only in an unrelated field, against the same
        // allowlist, produce the same decision.
        using var scope = _db.CreateScope();
        var mediator = CreateMediator(scope);
        var allowlist = new[] { new CapabilityGrant(CapabilityKind.ToolInvocation, "search") };

        var requestA = new CapabilityRequest(Guid.NewGuid(), CapabilityKind.ToolInvocation, "delete_everything", null);
        var requestB = new CapabilityRequest(Guid.NewGuid(), CapabilityKind.ToolInvocation, "delete_everything", Guid.NewGuid());

        var resultA = await mediator.AuthorizeAsync(requestA, allowlist);
        var resultB = await mediator.AuthorizeAsync(requestB, allowlist);

        Assert.False(resultA.IsAuthorized);
        Assert.False(resultB.IsAuthorized);
    }

    [Fact]
    public async Task Authorize_AuditsBothGrantsAndDenials()
    {
        using var scope = _db.CreateScope();
        var mediator = CreateMediator(scope);

        await mediator.AuthorizeAsync(
            new CapabilityRequest(Guid.NewGuid(), CapabilityKind.GitAccess, null, null),
            [new CapabilityGrant(CapabilityKind.GitAccess, null)]);
        await mediator.AuthorizeAsync(
            new CapabilityRequest(Guid.NewGuid(), CapabilityKind.GpuAccess, null, null),
            []);

        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        var events = await auditLog.ReadAsync(limit: 100);

        Assert.Contains(events, e => e.Kind == AuditEventKind.CapabilityGranted);
        Assert.Contains(events, e => e.Kind == AuditEventKind.AuthorizationDenied);
    }
}
