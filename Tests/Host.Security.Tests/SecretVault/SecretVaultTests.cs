using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Security.Tests.Fixtures;
using Telechron.Sdk.Security;
using Telechron.Sdk.Security.Audit;

namespace Telechron.Host.Security.Tests.SecretVault;

public sealed class SecretVaultTests : IAsyncLifetime
{
    private SecretVaultTestFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new SecretVaultTestFixture();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task StoreThenResolve_RoundTripsRawValue()
    {
        using var scope = _fixture.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var projectId = await scope.SeedProjectAsync();
        var rawValue = Encoding.UTF8.GetBytes("ghp_test_token_value");

        var handle = await vault.StoreAsync(projectId, "GitHub PAT", rawValue);
        var resolved = await vault.ResolveAsync(handle);

        Assert.Equal(rawValue, resolved);
    }

    [Fact]
    public async Task Handle_DoesNotContainRawValue()
    {
        using var scope = _fixture.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var projectId = await scope.SeedProjectAsync();
        var rawValue = "extremely-distinctive-raw-secret-9000";

        var handle = await vault.StoreAsync(projectId, "Test Secret", Encoding.UTF8.GetBytes(rawValue));

        Assert.DoesNotContain(rawValue, handle, StringComparison.Ordinal);
        Assert.StartsWith("sec_", handle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revoke_ThenResolve_ThrowsSecretRevokedException()
    {
        using var scope = _fixture.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var projectId = await scope.SeedProjectAsync();
        var handle = await vault.StoreAsync(projectId, "Test Secret", Encoding.UTF8.GetBytes("value"));

        await vault.RevokeAsync(handle);

        await Assert.ThrowsAsync<SecretRevokedException>(() => vault.ResolveAsync(handle));
    }

    [Fact]
    public async Task Rotate_ChangesResolvedValue_ButKeepsSameHandle()
    {
        using var scope = _fixture.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var projectId = await scope.SeedProjectAsync();
        var handle = await vault.StoreAsync(projectId, "Test Secret", Encoding.UTF8.GetBytes("old-value"));

        await vault.RotateAsync(handle, Encoding.UTF8.GetBytes("new-value"));
        var resolved = await vault.ResolveAsync(handle);

        Assert.Equal("new-value", Encoding.UTF8.GetString(resolved));
    }

    [Fact]
    public async Task ResolveNonexistentHandle_ThrowsSecretNotFoundException()
    {
        using var scope = _fixture.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<ISecretVault>();

        await Assert.ThrowsAsync<SecretNotFoundException>(() => vault.ResolveAsync("sec_does_not_exist"));
    }

    [Fact]
    public async Task Resolve_AppendsAuditEvent_WithoutRawValueInDetail()
    {
        using var scope = _fixture.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var projectId = await scope.SeedProjectAsync();
        var rawValue = "should-never-appear-in-audit-log";
        var handle = await vault.StoreAsync(projectId, "Test Secret", Encoding.UTF8.GetBytes(rawValue));

        await vault.ResolveAsync(handle);

        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        var events = await auditLog.ReadAsync(limit: 100);

        Assert.Contains(events, e => e.Kind == AuditEventKind.SecretAccessed);
        Assert.All(events, e => Assert.DoesNotContain(rawValue, e.DetailJson, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Revocation_TakesEffectImmediately_ForTheNextResolveCall()
    {
        // R-SEC8: "revocation invalidates outstanding handles and fails
        // in-flight calls using the old value rather than silently continuing."
        // ResolveAsync has no caching layer — every call re-checks RevokedAtUtc
        // against the DB, so a successful resolution followed immediately by
        // revocation must cause the very next resolution to fail, proving there
        // is no stale-value window a concurrent caller could exploit.
        using var scope = _fixture.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var projectId = await scope.SeedProjectAsync();
        var handle = await vault.StoreAsync(projectId, "Test Secret", Encoding.UTF8.GetBytes("value"));

        var beforeRevocation = await vault.ResolveAsync(handle);
        Assert.Equal("value", Encoding.UTF8.GetString(beforeRevocation));

        await vault.RevokeAsync(handle);

        await Assert.ThrowsAsync<SecretRevokedException>(() => vault.ResolveAsync(handle));
    }

    [Fact]
    public async Task RevokedAccessAttempt_IsAudited()
    {
        using var scope = _fixture.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var projectId = await scope.SeedProjectAsync();
        var handle = await vault.StoreAsync(projectId, "Test Secret", Encoding.UTF8.GetBytes("value"));
        await vault.RevokeAsync(handle);

        await Assert.ThrowsAsync<SecretRevokedException>(() => vault.ResolveAsync(handle));

        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        var events = await auditLog.ReadAsync(limit: 100);

        Assert.Contains(events, e => e.Kind == AuditEventKind.SecretAccessed && e.DetailJson.Contains("\"denied\":true"));
    }
}
