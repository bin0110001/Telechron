namespace Telechron.Host.Workflows.Tests;

using Telechron.Host.DesignDocs;
using Telechron.Host.Workflows.Approvals;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

public sealed class DesignDocumentManagerTests
{
    [Fact]
    public async Task ProposeRevisionAsync_RequiresHumanApproval_AndAppliesOnApproval()
    {
        var designDocRepo = new InMemoryDesignDocumentRepository();
        var reqRepo = new InMemoryRequirementRepository();
        var revRepo = new InMemoryRequirementRevisionRepository();
        var approvalManager = new ApprovalManager();

        var manager = new DesignDocumentManager(designDocRepo, reqRepo, revRepo, approvalManager);

        var docId = Guid.NewGuid();
        var req = new Requirement
        {
            Id = Guid.NewGuid(),
            DesignDocumentId = docId,
            RequirementId = "R-WF1",
            Title = "Original Requirement Title",
            Body = "Original requirement body",
            Status = RequirementStatus.Active,
            CurrentRevisionNumber = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        await reqRepo.AddAsync(req);

        var userId = Guid.NewGuid();
        var proposal = await manager.ProposeRevisionAsync(
            req.Id, "Updated Title", "Updated Body", "Clarified workflow specification", userId);

        Assert.NotNull(proposal);

        // Verify attempting to apply unapproved revision throws
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ApplyApprovedRevisionAsync(proposal, userId));

        // Submit approval via human gate (R-DM16b / R-SEC4)
        var approverId = Guid.NewGuid();
        await approvalManager.SubmitDecisionAsync(proposal.ApprovalRequestId, approverId, true, "Approved revision");

        // Apply approved revision
        var revision = await manager.ApplyApprovedRevisionAsync(proposal, approverId);

        Assert.NotNull(revision);
        Assert.Equal(2, revision.RevisionNumber);
        Assert.Equal("Updated Title", revision.Title);

        var updatedReq = await reqRepo.GetByIdAsync(req.Id);
        Assert.Equal(2, updatedReq?.CurrentRevisionNumber);
        Assert.Equal("Updated Title", updatedReq?.Title);
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

    private sealed class InMemoryRequirementRepository : IRequirementRepository
    {
        private readonly Dictionary<Guid, Requirement> _store = new();
        public Task<Requirement?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));
        public Task<IReadOnlyList<Requirement>> GetByDesignDocumentAsync(Guid designDocumentId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Requirement>>(_store.Values.Where(r => r.DesignDocumentId == designDocumentId).ToList());
        public Task<Requirement?> GetByRequirementIdAsync(Guid designDocumentId, string requirementId, CancellationToken ct = default) => Task.FromResult(_store.Values.FirstOrDefault(r => r.DesignDocumentId == designDocumentId && r.RequirementId == requirementId));
        public Task<IReadOnlyList<Requirement>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Requirement>>(_store.Values.ToList());
        public Task AddAsync(Requirement entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task UpdateAsync(Requirement entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { _store.Remove(id); return Task.CompletedTask; }
    }

    private sealed class InMemoryRequirementRevisionRepository : IRequirementRevisionRepository
    {
        private readonly Dictionary<Guid, RequirementRevision> _store = new();
        public Task<RequirementRevision?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));
        public Task<IReadOnlyList<RequirementRevision>> GetByRequirementAsync(Guid requirementId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RequirementRevision>>(_store.Values.Where(r => r.RequirementId == requirementId).ToList());
        public Task<IReadOnlyList<RequirementRevision>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RequirementRevision>>(_store.Values.ToList());
        public Task AddAsync(RequirementRevision entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task UpdateAsync(RequirementRevision entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { _store.Remove(id); return Task.CompletedTask; }
    }
}
