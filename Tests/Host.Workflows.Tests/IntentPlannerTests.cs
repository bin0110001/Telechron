namespace Telechron.Host.Workflows.Tests;

using Telechron.Host.Intent;
using Telechron.Host.Modules.Runtime;
using Telechron.Modules.CoreFunctions;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Persistence;

public sealed class IntentPlannerTests
{
    [Fact]
    public async Task CreatePlanAsync_DeterministicMatch_ProducesSideEffectFreePlan()
    {
        var runtime = new TestModuleRuntime();
        runtime.RegisterModule("telechron.functions.core", new CoreFunctionsModule());

        var gapAnalyzer = new CapabilityGapAnalyzer(runtime);
        var deterministic = new DeterministicIntentPlanner(new InMemoryWorkflowRepository(), gapAnalyzer);
        var llm = new LlmIntentPlanner(null, gapAnalyzer);
        var planRepo = new InMemoryIntentPlanRepository();
        var workflowRepo = new InMemoryWorkflowRepository();

        var planner = new IntentPlanner(deterministic, llm, planRepo, workflowRepo);

        var projectId = Guid.NewGuid();
        var plan = await planner.CreatePlanAsync(projectId, "Please zip the source directory");

        Assert.Equal(IntentPlanningPath.Deterministic, plan.PlanningPath);
        Assert.Null(plan.AppliedAtUtc);

        // Confirm plans themselves mutate no workflow state until applied
        var workflows = await workflowRepo.GetByProjectAsync(projectId);
        Assert.Empty(workflows);

        // Apply plan explicitly
        var appliedWorkflow = await planner.ApplyPlanAsync(plan.Id);
        Assert.NotNull(appliedWorkflow);

        var updatedPlan = await planRepo.GetByIdAsync(plan.Id);
        Assert.NotNull(updatedPlan?.AppliedAtUtc);
    }

    private sealed class TestModuleRuntime : IModuleRuntime
    {
        private readonly Dictionary<string, IModule> _modules = new();
        public void RegisterModule(string name, IModule module) => _modules[name] = module;
        public Task<LoadedModule> LoadAsync(string moduleAssemblyPath, CancellationToken ct = default) => throw new NotImplementedException();
        public LoadedModule? GetLoaded(string moduleName) => null;
        public TModule? GetLoadedAs<TModule>(string moduleName) where TModule : class, IModule => _modules.TryGetValue(moduleName, out var m) && m is TModule typed ? typed : null;
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
}
