namespace Telechron.Host.Workflows.Tests;

using Telechron.Host.Intent;
using Telechron.Host.Modules.Runtime;
using Telechron.Host.Synthesis;
using Telechron.Host.Workflows.Approvals;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Persistence;

public sealed class CapabilityGapApprovalFlowTests
{
    [Fact]
    public async Task ProcessRequestAsync_MissingCapability_RequiresHumanApproval()
    {
        var runtime = new TestModuleRuntime(); // No modules registered -> missing capabilities
        var gapAnalyzer = new CapabilityGapAnalyzer(runtime);
        var deterministic = new DeterministicIntentPlanner(new InMemoryWorkflowRepository(), gapAnalyzer);
        var llm = new LlmIntentPlanner(null, gapAnalyzer);
        var planRepo = new InMemoryIntentPlanRepository();
        var workflowRepo = new InMemoryWorkflowRepository();
        var planner = new IntentPlanner(deterministic, llm, planRepo, workflowRepo);
        var approvalManager = new ApprovalManager();
        var synthesizer = new CapabilitySynthesizer(null);
        var verificationRunner = new CapabilityVerificationRunner(null);
        var designDocRepo = new InMemoryDesignDocumentRepository();

        var flow = new CapabilityGapApprovalFlow(
            planner, gapAnalyzer, approvalManager, synthesizer, verificationRunner, designDocRepo);

        var projectId = Guid.NewGuid();
        var result = await flow.ProcessRequestAsync(projectId, "Deploy to staging environment");

        Assert.True(result.RequiresHumanApproval);
        Assert.NotNull(result.ApprovalRequest);
        Assert.False(result.ApprovalRequest.IsSatisfied);

        // Submit approval
        var userId = Guid.NewGuid();
        await approvalManager.SubmitDecisionAsync(result.ApprovalRequest.Id, userId, true, "Approved deployment capability synthesis");

        // Execute synthesis after human gate approval
        var synthesisResult = await flow.ExecuteSynthesisAfterApprovalAsync(projectId, result.Plan, result.ApprovalRequest.Id);

        Assert.NotNull(synthesisResult.SynthesizedModule);
        Assert.True(synthesisResult.SynthesizedModule.Success);
        Assert.NotNull(synthesisResult.VerificationResult);
        Assert.True(synthesisResult.VerificationResult.Success);
    }

    private sealed class TestModuleRuntime : IModuleRuntime
    {
        public Task<LoadedModule> LoadAsync(string moduleAssemblyPath, CancellationToken ct = default) => throw new NotImplementedException();
        public LoadedModule? GetLoaded(string moduleName) => null;
        public TModule? GetLoadedAs<TModule>(string moduleName) where TModule : class, IModule => null;
        public Task<ModuleUnloadResult> UnloadAsync(string moduleName, CancellationToken ct = default) => Task.FromResult(new ModuleUnloadResult(true, false));
    }

    private sealed class InMemoryIntentPlanRepository : IIntentPlanRepository
    {
        private readonly Dictionary<Guid, IntentPlan> _store = new();
        public Task<IntentPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));
        public Task<IReadOnlyList<IntentPlan>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<IntentPlan>>(_store.Values.Where(p => p.ProjectId == projectId).ToList());
        public Task<IReadOnlyList<IntentPlan>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<IntentPlan>>(_store.Values.ToList());
        public Task AddAsync(IntentPlan entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task UpdateAsync(IntentPlan entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { _store.Remove(id); return Task.CompletedTask; }
    }

    private sealed class InMemoryWorkflowRepository : IWorkflowRepository
    {
        private readonly Dictionary<Guid, Workflow> _store = new();
        public Task<Workflow?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));
        public Task<IReadOnlyList<Workflow>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Workflow>>(_store.Values.Where(w => w.ProjectId == projectId).ToList());
        public Task<IReadOnlyList<Workflow>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Workflow>>(_store.Values.ToList());
        public Task AddAsync(Workflow entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task UpdateAsync(Workflow entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { _store.Remove(id); return Task.CompletedTask; }
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
