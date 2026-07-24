namespace Telechron.Host.Workflows.Tests;

using System.Text.Json;
using Telechron.Host.Intent;
using Telechron.Host.Llm;
using Telechron.Host.Modules.Runtime;
using Telechron.Modules.CoreFunctions;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Modules.Llm;
using Telechron.Sdk.Persistence;

public sealed class IntentPlannerTests
{
    private sealed class FakeLlmDispatcher(Func<LlmCompletionResult> respond) : ILlmDispatcher
    {
        public int CallCount { get; private set; }
        public Task<LlmCompletionResult> DispatchAsync(LlmConnection connection, Guid? projectId, LlmCompletionRequest request, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(respond());
        }

        public static FakeLlmDispatcher ProducingWorkflow(string functionKind) => new(() =>
        {
            var envelope = JsonSerializer.Serialize(new
            {
                workflowName = "LLM Generated Workflow",
                steps = new[]
                {
                    new { id = "step-1", name = $"Execute {functionKind}", functionKind, parameters = new Dictionary<string, string>() },
                },
            });
            return new LlmCompletionResult(true, envelope, "fake-model", 5, 5, null);
        });
    }

    private static (IProjectRepository projects, ILlmConnectionRepository connections, Guid projectId) SeedProjectWithLlmConnection()
    {
        var projectId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        var connections = new InMemoryLlmConnectionRepository();
        connections.AddAsync(new LlmConnection { Id = connectionId, Name = "fake", Provider = "fake", ConfigurationJson = "{}", CreatedAtUtc = DateTimeOffset.UtcNow }).GetAwaiter().GetResult();

        var projects = new InMemoryProjectRepository();
        projects.AddAsync(new Project
        {
            Id = projectId, Name = "Test", RootPath = "/repo", OwnerUserId = Guid.NewGuid(),
            RepairPolicy = RepairPolicy.RequireApproval, LlmConnectionId = connectionId, CreatedAtUtc = DateTimeOffset.UtcNow,
        }).GetAwaiter().GetResult();

        return (projects, connections, projectId);
    }

    [Fact]
    public async Task CreatePlanAsync_DeterministicMatch_ProducesSideEffectFreePlan()
    {
        var runtime = new TestModuleRuntime();
        runtime.RegisterModule("telechron.functions.core", new CoreFunctionsModule());

        var gapAnalyzer = new CapabilityGapAnalyzer(runtime);
        var deterministic = new DeterministicIntentPlanner(new InMemoryWorkflowRepository(), gapAnalyzer);
        var (projects, connections, seededProjectId) = SeedProjectWithLlmConnection();
        var llm = new LlmIntentPlanner(new FakeLlmDispatcher(() => LlmCompletionResult.Failure("n/a", "not exercised")), connections, projects, gapAnalyzer);
        var planRepo = new InMemoryIntentPlanRepository();
        var workflowRepo = new InMemoryWorkflowRepository();

        var planner = new IntentPlanner(deterministic, llm, planRepo, workflowRepo);

        var plan = await planner.CreatePlanAsync(seededProjectId, "Please zip the source directory");

        Assert.Equal(IntentPlanningPath.Deterministic, plan.PlanningPath);
        Assert.Null(plan.AppliedAtUtc);

        // Confirm plans themselves mutate no workflow state until applied
        var workflows = await workflowRepo.GetByProjectAsync(seededProjectId);
        Assert.Empty(workflows);

        // Apply plan explicitly
        var appliedWorkflow = await planner.ApplyPlanAsync(plan.Id);
        Assert.NotNull(appliedWorkflow);

        var updatedPlan = await planRepo.GetByIdAsync(plan.Id);
        Assert.NotNull(updatedPlan?.AppliedAtUtc);
    }

    // R-BUILD1: proves the fallback path genuinely calls the LLM and uses
    // its response, rather than a second layer of hardcoded keyword
    // matching mislabeled as "LLM-driven."
    [Fact]
    public async Task CreatePlanAsync_NoDeterministicMatch_CallsLlmAndUsesItsWorkflowDefinition()
    {
        var runtime = new TestModuleRuntime();
        var gapAnalyzer = new CapabilityGapAnalyzer(runtime);
        var deterministic = new DeterministicIntentPlanner(new InMemoryWorkflowRepository(), gapAnalyzer);
        var (projects, connections, seededProjectId) = SeedProjectWithLlmConnection();
        var fakeLlm = FakeLlmDispatcher.ProducingWorkflow("custom-deploy-target");
        var llm = new LlmIntentPlanner(fakeLlm, connections, projects, gapAnalyzer);
        var planRepo = new InMemoryIntentPlanRepository();
        var workflowRepo = new InMemoryWorkflowRepository();

        var planner = new IntentPlanner(deterministic, llm, planRepo, workflowRepo);

        var plan = await planner.CreatePlanAsync(seededProjectId, "do something nobody has a rule for");

        Assert.Equal(1, fakeLlm.CallCount);
        Assert.Equal(IntentPlanningPath.PersonaDriven, plan.PlanningPath);
        Assert.Contains("custom-deploy-target", plan.ProposedWorkflowIdsJson);
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

    private sealed class InMemoryProjectRepository : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _store = new();
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));
        public Task<IReadOnlyList<Project>> GetByOwnerAsync(Guid ownerUserId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Project>>(_store.Values.Where(p => p.OwnerUserId == ownerUserId).ToList());
        public Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Project>>(_store.Values.ToList());
        public Task AddAsync(Project entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task UpdateAsync(Project entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { _store.Remove(id); return Task.CompletedTask; }
    }

    private sealed class InMemoryLlmConnectionRepository : ILlmConnectionRepository
    {
        private readonly Dictionary<Guid, LlmConnection> _store = new();
        public Task<LlmConnection?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));
        public Task<IReadOnlyList<LlmConnection>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<LlmConnection>>(_store.Values.ToList());
        public Task AddAsync(LlmConnection entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task UpdateAsync(LlmConnection entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { _store.Remove(id); return Task.CompletedTask; }
    }
}
