namespace Telechron.Host.Workflows.Tests;

using System.Text.Json;
using Telechron.Host.Intent;
using Telechron.Host.Llm;
using Telechron.Host.Modules;
using Telechron.Host.Modules.Runtime;
using Telechron.Host.Repair;
using Telechron.Host.Synthesis;
using Telechron.Host.Workflows.Approvals;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Modules.Llm;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Repair;
using Telechron.Sdk.Security;
using Telechron.Sdk.Synthesis;

public sealed class CapabilityGapApprovalFlowTests
{
    // Controllable stand-ins for the two seams that genuinely need external
    // infrastructure (a real LLM, a real Agent/container) to exercise for
    // real -- same split Phase 7's RepairPipelineOrchestratorTests uses.
    // Everything else here (approval gate enforcement, capability capping,
    // Requirement threading, drift check wiring) is the REAL implementation.
    private sealed class FakeLlmDispatcher(Func<LlmCompletionResult> respond) : ILlmDispatcher
    {
        public Task<LlmCompletionResult> DispatchAsync(LlmConnection connection, Guid? projectId, LlmCompletionRequest request, CancellationToken ct = default) =>
            Task.FromResult(respond());

        public static FakeLlmDispatcher ProducingModule(string moduleName, IReadOnlyList<string> declaredCapabilities) => new(() =>
        {
            var envelope = JsonSerializer.Serialize(new
            {
                moduleSource = $"public sealed class {moduleName} {{ }}",
                selfTestSource = "public class SelfTestFake { [Xunit.Fact] public void Test() => Xunit.Assert.True(true); }",
                declaredCapabilities,
            });
            return new LlmCompletionResult(true, envelope, "fake-model", 10, 10, null);
        });
    }

    private sealed class FakeDispatchQueue(Func<CommandValidationResult> onEnqueue) : IDispatchQueue
    {
        public CommandValidationResult Enqueue(Guid machineId, DispatchedCommand command) => onEnqueue();
        public IAsyncEnumerable<DispatchedCommand> ReadAllAsync(Guid machineId, CancellationToken ct) =>
            throw new NotSupportedException("Not exercised by this test.");
    }

    private sealed class FakeCommandResultCorrelator(Func<CommandOutcome> respond) : ICommandResultCorrelator
    {
        public Task<CommandOutcome> AwaitResultAsync(Guid commandId, Func<Task> dispatch, TimeSpan timeout, CancellationToken ct = default)
        {
            dispatch();
            return Task.FromResult(respond());
        }

        public void Complete(CommandOutcome outcome) { }

        // A successful synthesis build reports the built assembly's blob
        // ref as its OutputSummary (CapabilityVerificationRunner's real
        // contract with RunCapabilitySynthesisBuildCommandHandler).
        public static FakeCommandResultCorrelator SucceedingWithBlobRef(string blobRef) =>
            new(() => new CommandOutcome(Guid.NewGuid(), true, blobRef, string.Empty));
    }

    private sealed class NullContainerExecutionService : Telechron.Sdk.Containers.IContainerExecutionService
    {
        public Task<Telechron.Sdk.Containers.ContainerExecutionResult> ExecuteAsync(Telechron.Sdk.Containers.ContainerExecutionRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException("Not exercised by this test.");
    }

    private static IArtifactBlobStore NewBlobStore() =>
        new Telechron.Host.Persistence.FilesystemArtifactBlobStore(
            Path.Combine(Path.GetTempPath(), "telechron-synthesis-tests-" + Guid.NewGuid().ToString("N")));

    private static CapabilityGapApprovalFlow BuildFlow(
        out ApprovalManager approvalManager, out InMemoryIntentPlanRepository planRepo, out Guid projectId,
        IReadOnlyList<string> llmDeclaredCapabilities, bool seedFakeBuiltAssembly)
    {
        var runtime = new TestModuleRuntime(); // No modules registered -> missing capabilities
        var gapAnalyzer = new CapabilityGapAnalyzer(runtime);
        var deterministic = new DeterministicIntentPlanner(new InMemoryWorkflowRepository(), gapAnalyzer);
        var connection = new LlmConnection { Id = Guid.NewGuid(), Name = "fake", Provider = "fake", ConfigurationJson = "{}", CreatedAtUtc = DateTimeOffset.UtcNow };
        var projectRepo = new InMemoryProjectRepository();
        var llmConnectionRepo = new InMemoryLlmConnectionRepository();
        llmConnectionRepo.AddAsync(connection).GetAwaiter().GetResult();

        projectId = Guid.NewGuid();
        projectRepo.AddAsync(new Project
        {
            Id = projectId, Name = "Test", RootPath = "/repo", OwnerUserId = Guid.NewGuid(),
            RepairPolicy = RepairPolicy.RequireApproval, LlmConnectionId = connection.Id, CreatedAtUtc = DateTimeOffset.UtcNow,
        }).GetAwaiter().GetResult();

        var llm = new LlmIntentPlanner(
            new FakeLlmDispatcher(() => new LlmCompletionResult(false, string.Empty, "n/a", 0, 0, "not exercised by this test")),
            llmConnectionRepo, projectRepo, gapAnalyzer);
        planRepo = new InMemoryIntentPlanRepository();
        var workflowRepo = new InMemoryWorkflowRepository();
        var planner = new IntentPlanner(deterministic, llm, planRepo, workflowRepo);
        approvalManager = new ApprovalManager();

        // CapabilityGapApprovalFlow resolves its own LlmConnection per-call
        // and constructs CapabilitySynthesizer internally (same reasoning as
        // RepairPipelineFactory) -- this fake dispatcher stands in for
        // whichever LLM call that internal construction ends up making.
        var synthesisLlmDispatcher = FakeLlmDispatcher.ProducingModule("SynthesizedTestModule", llmDeclaredCapabilities);

        var blobStore = NewBlobStore();

        // Stands in for what RunCapabilitySynthesisBuildCommandHandler
        // would really produce on the Agent (a compiled DLL) and upload
        // back via StoreArtifact -- this test fakes the dispatch/build
        // round trip, but the blob it hands back must be a real, readable
        // blob for CapabilityVerificationRunner's later OpenReadAsync to
        // succeed against, same as a real build's output would be.
        var builtAssemblyBlobRef = string.Empty;
        if (seedFakeBuiltAssembly)
        {
            using var fakeAssemblyBytes = new MemoryStream([0x4D, 0x5A]); // minimal placeholder bytes
            builtAssemblyBlobRef = blobStore.SaveAsync(fakeAssemblyBytes, "SynthesizedTestModule.dll").GetAwaiter().GetResult();
        }

        var moduleRepo = new InMemoryModuleRepository();
        var trustEvaluator = new AlwaysTrustedEvaluator();

        // No Requirements are seeded in these tests, so
        // ArchitecturalDriftDetector.CheckAsync short-circuits to
        // "no drift" without ever calling the LLM (its own real,
        // documented behavior when activeRequirements.Count == 0) --
        // this dispatcher stands in for that call in case a future test
        // seeds Requirements and exercises the real LLM-backed path.
        var verificationRunner = new CapabilityVerificationRunner(
            blobStore,
            new FakeDispatchQueue(() => CommandValidationResult.Valid),
            FakeCommandResultCorrelator.SucceedingWithBlobRef(builtAssemblyBlobRef),
            new SynthesisIntegritySigner(Microsoft.Extensions.Options.Options.Create(new SynthesisIntegritySignerOptions())),
            trustEvaluator,
            synthesisLlmDispatcher,
            llmConnectionRepo,
            projectRepo);

        var designDocRepo = new InMemoryDesignDocumentRepository();
        var requirementRepo = new InMemoryRequirementRepository();

        return new CapabilityGapApprovalFlow(
            planner, gapAnalyzer, approvalManager, synthesisLlmDispatcher, projectRepo, llmConnectionRepo,
            verificationRunner, designDocRepo, requirementRepo);
    }

    [Fact]
    public async Task ProcessRequestAsync_MissingCapability_RequiresHumanApproval()
    {
        var flow = BuildFlow(out var approvalManager, out _, out var projectId, ["FilesystemRead"], false);
        var result = await flow.ProcessRequestAsync(projectId, "Deploy to staging environment");

        Assert.True(result.RequiresHumanApproval);
        Assert.NotNull(result.ApprovalRequest);
        Assert.False(result.ApprovalRequest.IsSatisfied);
    }

    // The gap this test closes: the original version of this test never
    // proved synthesis is actually blocked without approval -- it only
    // asserted the pre-approval state, then jumped straight to the happy
    // path. This proves ExecuteSynthesisAfterApprovalAsync really throws.
    [Fact]
    public async Task ExecuteSynthesisAfterApprovalAsync_WithoutApproval_Throws()
    {
        var flow = BuildFlow(out var approvalManager, out _, out var projectId, ["FilesystemRead"], false);
        var result = await flow.ProcessRequestAsync(projectId, "Deploy to staging environment");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            flow.ExecuteSynthesisAfterApprovalAsync(projectId, result.Plan, result.ApprovalRequest!.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task ExecuteSynthesisAfterApprovalAsync_ApprovedWithCapabilities_SynthesizesAndVerifies()
    {
        var flow = BuildFlow(out var approvalManager, out _, out var projectId, ["FilesystemRead"], true);
        var result = await flow.ProcessRequestAsync(projectId, "Deploy to staging environment");

        var userId = Guid.NewGuid();
        var approvedCapabilitiesJson = System.Text.Json.JsonSerializer.Serialize(new[] { "FilesystemRead" });
        await approvalManager.SubmitDecisionAsync(
            result.ApprovalRequest!.Id, userId, true, "Approved deployment capability synthesis", approvedCapabilitiesJson);

        var synthesisResult = await flow.ExecuteSynthesisAfterApprovalAsync(projectId, result.Plan, result.ApprovalRequest.Id, Guid.NewGuid());

        Assert.NotNull(synthesisResult.SynthesizedModule);
        Assert.True(synthesisResult.SynthesizedModule.Success);
        Assert.Equal(["FilesystemRead"], synthesisResult.SynthesizedModule.DeclaredCapabilities);
        Assert.NotNull(synthesisResult.VerificationResult);
        Assert.True(synthesisResult.VerificationResult.Success);
    }

    // R-MOD8a: the LLM declaring a capability the human approval never
    // granted must be filtered out server-side, not trusted at face value.
    [Fact]
    public async Task ExecuteSynthesisAfterApprovalAsync_LlmDeclaresUnapprovedCapability_IsFilteredOut()
    {
        var flow = BuildFlow(out var approvalManager, out _, out var projectId, ["FilesystemRead", "InternetAccess"], true);
        var result = await flow.ProcessRequestAsync(projectId, "Deploy to staging environment");

        var userId = Guid.NewGuid();
        // Human only approves FilesystemRead -- LLM (per BuildFlow's fake)
        // also declares InternetAccess, which must not survive.
        var approvedCapabilitiesJson = System.Text.Json.JsonSerializer.Serialize(new[] { "FilesystemRead" });
        await approvalManager.SubmitDecisionAsync(result.ApprovalRequest!.Id, userId, true, "Approved", approvedCapabilitiesJson);

        var synthesisResult = await flow.ExecuteSynthesisAfterApprovalAsync(projectId, result.Plan, result.ApprovalRequest.Id, Guid.NewGuid());

        Assert.Equal(["FilesystemRead"], synthesisResult.SynthesizedModule!.DeclaredCapabilities);
        Assert.DoesNotContain("InternetAccess", synthesisResult.SynthesizedModule.DeclaredCapabilities);
    }

    private sealed class AlwaysTrustedEvaluator : IModuleTrustEvaluator
    {
        public Task<ModuleTrustResult> EvaluateAsync(
            Guid projectId, string moduleName, Guid machineId, string toolchainImageDigest, string candidateAssemblyPath,
            Telechron.Sdk.Modules.ModuleIntegrityManifest integrityManifest, IReadOnlyList<string> declaredCapabilities,
            IReadOnlyList<string> approvedCapabilities, string? priorInstalledAssemblyPath,
            (ModuleVersion Installed, ModuleVersion Candidate)? versionTransition = null, bool versionReapproved = false,
            CancellationToken ct = default) =>
            Task.FromResult(new ModuleTrustResult(ModuleTrustOutcome.Trusted, "trusted (test double)"));
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

    private sealed class InMemoryModuleRepository : IModuleRepository
    {
        private readonly Dictionary<Guid, Module> _store = new();
        public Task<Module?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));
        public Task<Module?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(_store.Values.FirstOrDefault(m => m.Name == name));
        public Task<IReadOnlyList<Module>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Module>>(_store.Values.ToList());
        public Task AddAsync(Module entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task UpdateAsync(Module entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { _store.Remove(id); return Task.CompletedTask; }
    }

    private sealed class InMemoryRequirementRepository : IRequirementRepository
    {
        private readonly Dictionary<Guid, Requirement> _store = new();
        public Task<Requirement?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));
        public Task<IReadOnlyList<Requirement>> GetByDesignDocumentAsync(Guid designDocumentId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Requirement>>(_store.Values.Where(r => r.DesignDocumentId == designDocumentId).ToList());
        public Task<Requirement?> GetByRequirementIdAsync(Guid designDocumentId, string requirementId, CancellationToken ct = default) =>
            Task.FromResult(_store.Values.FirstOrDefault(r => r.DesignDocumentId == designDocumentId && r.RequirementId == requirementId));
        public Task<IReadOnlyList<Requirement>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Requirement>>(_store.Values.ToList());
        public Task AddAsync(Requirement entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task UpdateAsync(Requirement entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { _store.Remove(id); return Task.CompletedTask; }
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
