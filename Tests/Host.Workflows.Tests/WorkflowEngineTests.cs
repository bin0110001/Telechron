namespace Telechron.Host.Workflows.Tests;

using System.Text.Json;
using Telechron.Host.Modules.Runtime;
using Telechron.Host.Workflows;
using Telechron.Host.Workflows.Approvals;
using Telechron.Modules.CoreFunctions;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Workflows;

public sealed class WorkflowEngineTests
{
    private readonly InMemoryWorkflowRepository _workflowRepo = new();
    private readonly InMemoryWorkflowRunRepository _runRepo = new();
    private readonly InMemoryArtifactRepository _artifactRepo = new();
    private readonly ApprovalManager _approvalManager = new();

    private readonly TestModuleRuntime _moduleRuntime = new();

    [Fact]
    public async Task ExecuteRunAsync_LinearWorkflow_ExecutesSuccessfully()
    {
        _moduleRuntime.RegisterModule("telechron.functions.core", new CoreFunctionsModule());

        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Name = "Linear Zip Test Workflow",
            DefinitionJson = JsonSerializer.Serialize(new WorkflowDefinition
            {
                Name = "Zip Definition",
                FailurePolicy = WorkflowFailurePolicy.FailFast,
                Steps =
                [
                    new WorkflowStepDefinition
                    {
                        Id = "step-1",
                        Name = "Zip Step",
                        FunctionKind = "zip",
                        Parameters = new Dictionary<string, string>
                        {
                            ["sourceDirectory"] = "${sourceDirectory}",
                            ["destinationZipPath"] = "${destinationZipPath}"
                        }
                    }
                ]
            }),
            FailurePolicy = WorkflowFailurePolicy.FailFast,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        await _workflowRepo.AddAsync(workflow);

        var engine = new WorkflowEngine(_workflowRepo, _runRepo, _artifactRepo, _approvalManager, _moduleRuntime);

        var tempSource = Path.Combine(Path.GetTempPath(), "telechron_wf_src_" + Guid.NewGuid());
        var tempZip = Path.Combine(Path.GetTempPath(), "telechron_wf_out_" + Guid.NewGuid() + ".zip");
        Directory.CreateDirectory(tempSource);
        File.WriteAllText(Path.Combine(tempSource, "test.txt"), "hello world");

        try
        {
            var run = await engine.StartWorkflowAsync(workflow.Id, new Dictionary<string, string>
            {
                ["sourceDirectory"] = tempSource,
                ["destinationZipPath"] = tempZip
            });

            Assert.Equal(WorkflowRunStatus.Passed, run.Status);
            Assert.NotNull(run.CompletedAtUtc);
            Assert.True(File.Exists(tempZip));
        }
        finally
        {
            if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true);
            if (File.Exists(tempZip)) File.Delete(tempZip);
        }
    }

    [Fact]
    public async Task ExecuteRunAsync_ApprovalGate_PausesAndResumesOnApproval()
    {
        _moduleRuntime.RegisterModule("telechron.functions.core", new CoreFunctionsModule());

        var tempSource = Path.Combine(Path.GetTempPath(), "telechron_wf_src2_" + Guid.NewGuid());
        var tempZip = Path.Combine(Path.GetTempPath(), "telechron_wf_out2_" + Guid.NewGuid() + ".zip");
        Directory.CreateDirectory(tempSource);
        File.WriteAllText(Path.Combine(tempSource, "data.txt"), "approval test");

        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Name = "Approval Gate Workflow",
            DefinitionJson = JsonSerializer.Serialize(new WorkflowDefinition
            {
                Name = "Gated Definition",
                FailurePolicy = WorkflowFailurePolicy.FailFast,
                Steps =
                [
                    new WorkflowStepDefinition
                    {
                        Id = "gated-step",
                        Name = "Gated Zip Step",
                        FunctionKind = "zip",
                        Parameters = new Dictionary<string, string>
                        {
                            ["sourceDirectory"] = "${sourceDirectory}",
                            ["destinationZipPath"] = "${destinationZipPath}"
                        },
                        ApprovalGate = new ApprovalGateDefinition
                        {
                            GateId = "gate-1",
                            Prompt = "Approve zip execution?"
                        }
                    }
                ]
            }),
            FailurePolicy = WorkflowFailurePolicy.FailFast,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        await _workflowRepo.AddAsync(workflow);

        var engine = new WorkflowEngine(_workflowRepo, _runRepo, _artifactRepo, _approvalManager, _moduleRuntime);

        try
        {
            // Initial execution pauses at approval gate
            var initialRun = await engine.StartWorkflowAsync(workflow.Id, new Dictionary<string, string>
            {
                ["sourceDirectory"] = tempSource,
                ["destinationZipPath"] = tempZip
            });
            Assert.Equal(WorkflowRunStatus.AwaitingApproval, initialRun.Status);

            var pending = await _approvalManager.GetPendingRequestsAsync();
            Assert.Single(pending);
            var req = pending[0];
            Assert.Equal("gated-step", req.StepId);

            // Submit approval decision
            var userId = Guid.NewGuid();
            await _approvalManager.SubmitDecisionAsync(req.Id, userId, true, "Looks good");

            // Resume execution
            var resumedRun = await engine.ResumeRunAsync(initialRun.Id, req.Id);
            Assert.Equal(WorkflowRunStatus.Passed, resumedRun.Status);
            Assert.True(File.Exists(tempZip));
        }
        finally
        {
            if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true);
            if (File.Exists(tempZip)) File.Delete(tempZip);
        }
    }

    private sealed class TestModuleRuntime : IModuleRuntime
    {
        private readonly Dictionary<string, IModule> _modules = new();

        public void RegisterModule(string name, IModule module) => _modules[name] = module;

        public Task<LoadedModule> LoadAsync(string moduleAssemblyPath, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public LoadedModule? GetLoaded(string moduleName) => null;

        public TModule? GetLoadedAs<TModule>(string moduleName) where TModule : class, IModule =>
            _modules.TryGetValue(moduleName, out var m) && m is TModule typed ? typed : null;

        public Task<ModuleUnloadResult> UnloadAsync(string moduleName, CancellationToken ct = default) =>
            Task.FromResult(new ModuleUnloadResult(true, false));
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

    private sealed class InMemoryWorkflowRunRepository : IWorkflowRunRepository
    {
        private readonly Dictionary<Guid, WorkflowRun> _store = new();
        public Task<WorkflowRun?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));
        public Task<IReadOnlyList<WorkflowRun>> GetByWorkflowAsync(Guid workflowId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkflowRun>>(_store.Values.Where(r => r.WorkflowId == workflowId).ToList());
        public Task<IReadOnlyList<WorkflowRun>> GetActiveAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkflowRun>>(_store.Values.Where(r => r.Status is WorkflowRunStatus.Pending or WorkflowRunStatus.Running or WorkflowRunStatus.AwaitingApproval).ToList());
        public Task<IReadOnlyList<WorkflowRun>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkflowRun>>(_store.Values.ToList());
        public Task AddAsync(WorkflowRun entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task UpdateAsync(WorkflowRun entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { _store.Remove(id); return Task.CompletedTask; }
    }

    private sealed class InMemoryArtifactRepository : IArtifactRepository
    {
        private readonly Dictionary<Guid, Artifact> _store = new();
        public Task<Artifact?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));
        public Task<IReadOnlyList<Artifact>> GetByWorkflowRunAsync(Guid workflowRunId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Artifact>>(_store.Values.Where(a => a.WorkflowRunId == workflowRunId).ToList());
        public Task<IReadOnlyList<Artifact>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Artifact>>(_store.Values.ToList());
        public Task AddAsync(Artifact entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task UpdateAsync(Artifact entity, CancellationToken ct = default) { _store[entity.Id] = entity; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { _store.Remove(id); return Task.CompletedTask; }
    }
}
