using Telechron.Host.Modules.Permissions;
using Telechron.Host.Security.Permissions;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Security.Audit;
using Telechron.Sdk.Security.Permissions;

namespace Telechron.Host.Modules.Tests;

public class ModuleCapabilityMediatorTests
{
    private sealed class InMemoryAuditLog : IAuditLog
    {
        public List<(AuditEventKind Kind, string DetailJson)> Entries { get; } = [];

        public Task<AuditEvent> AppendAsync(
            AuditEventKind kind, string detailJson, Guid? actorUserId = null, Guid? projectId = null, CancellationToken ct = default)
        {
            Entries.Add((kind, detailJson));
            return Task.FromResult(new AuditEvent
            {
                Sequence = Entries.Count,
                Kind = kind,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                ActorUserId = actorUserId,
                ProjectId = projectId,
                DetailJson = detailJson,
                PriorHash = "",
                RecordHash = "",
            });
        }

        public Task<IReadOnlyList<AuditEvent>> ReadAsync(long fromSequence = 0, int limit = 100, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AuditEvent>>([]);

        public Task<AuditVerificationResult> VerifyChainAsync(CancellationToken ct = default) =>
            Task.FromResult(new AuditVerificationResult(true, null));
    }

    private static Module CreateModule(IReadOnlyList<CapabilityGrant> approvedGrants) => new()
    {
        Id = Guid.NewGuid(),
        Name = "test-module",
        Kind = "sample",
        VersionMajor = 1,
        VersionMinor = 0,
        VersionPatch = 0,
        CapabilitiesJson = ModuleCapabilities.Serialize(approvedGrants),
        TestCommand = "dummy",
        SourceCodeRef = "dummy-ref",
        InstalledAtUtc = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task AuthorizeAsync_CapabilityInModuleApprovedSet_IsAuthorized()
    {
        var module = CreateModule([new CapabilityGrant(CapabilityKind.FilesystemRead, null)]);
        var mediator = new ModuleCapabilityMediator(new PermissionMediator(new InMemoryAuditLog()));

        var result = await mediator.AuthorizeAsync(module, Guid.NewGuid(), CapabilityKind.FilesystemRead);

        Assert.True(result.IsAuthorized);
    }

    [Fact]
    public async Task AuthorizeAsync_CapabilityNotInModuleApprovedSet_IsDenied()
    {
        var module = CreateModule([new CapabilityGrant(CapabilityKind.FilesystemRead, null)]);
        var mediator = new ModuleCapabilityMediator(new PermissionMediator(new InMemoryAuditLog()));

        var result = await mediator.AuthorizeAsync(module, Guid.NewGuid(), CapabilityKind.InternetAccess);

        Assert.False(result.IsAuthorized);
    }

    [Fact]
    public async Task AuthorizeAsync_ResourceScopedGrant_OnlyAuthorizesThatResource()
    {
        var module = CreateModule([new CapabilityGrant(CapabilityKind.SecretAccess, "handle-a")]);
        var mediator = new ModuleCapabilityMediator(new PermissionMediator(new InMemoryAuditLog()));

        var authorizedForA = await mediator.AuthorizeAsync(module, Guid.NewGuid(), CapabilityKind.SecretAccess, "handle-a");
        var deniedForB = await mediator.AuthorizeAsync(module, Guid.NewGuid(), CapabilityKind.SecretAccess, "handle-b");

        Assert.True(authorizedForA.IsAuthorized);
        Assert.False(deniedForB.IsAuthorized);
    }

    [Fact]
    public async Task AuthorizeAsync_EveryDecision_IsAudited()
    {
        var module = CreateModule([]);
        var auditLog = new InMemoryAuditLog();
        var mediator = new ModuleCapabilityMediator(new PermissionMediator(auditLog));

        await mediator.AuthorizeAsync(module, Guid.NewGuid(), CapabilityKind.GpuAccess);

        Assert.Single(auditLog.Entries);
        Assert.Equal(AuditEventKind.AuthorizationDenied, auditLog.Entries[0].Kind);
    }
}
