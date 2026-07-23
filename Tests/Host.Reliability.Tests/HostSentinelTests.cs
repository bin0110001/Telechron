namespace Telechron.Host.Reliability.Tests;

using Telechron.Host.DesignDocs;
using Telechron.Host.Reliability;
using Telechron.Host.Workflows.Approvals;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

public sealed class HostSentinelTests
{
    [Fact]
    public async Task RunSelfRepairCheckAsync_HostSelfRepair_AlwaysRequiresHumanApproval()
    {
        var designDocRepo = new InMemoryDesignDocumentRepository();
        var reflexiveWire = new ReflexiveDesignDocWire(designDocRepo);
        var approvalManager = new ApprovalManager();

        var sentinel = new HostSentinel(reflexiveWire, null, approvalManager);

        var report = await sentinel.RunSelfRepairCheckAsync();

        Assert.True(report.RepairNeeded);
        Assert.True(report.RequiresHumanApproval);
        Assert.NotNull(report.RepairAttempt);
        Assert.Null(report.RepairAttempt.ApprovalDecision);

        var pending = await approvalManager.GetPendingRequestsAsync();
        Assert.Single(pending);
        Assert.Equal("R-SEC4-privileged-path", pending[0].GateId);
    }

    private sealed class InMemoryDesignDocumentRepository : IDesignDocumentRepository
    {
        private readonly Dictionary<Guid, DesignDocument> _store = new();
        public Task<DesignDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));
        public Task<DesignDocument?> GetByProjectAsync(Guid projectId, CancellationToken ct = default) => Task.FromResult(_store.Values.FirstOrDefault(d => d.ProjectId == projectId));
        public Task<IReadOnlyList<DesignDocument>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DesignDocument>>(_store.Values.ToList());
        public Task AddAsync(DesignDocument entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task UpdateAsync(DesignDocument entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { _store.Remove(id); return Task.CompletedTask; }
    }
}
