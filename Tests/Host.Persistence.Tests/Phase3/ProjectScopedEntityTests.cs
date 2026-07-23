using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Tests.Phase3;

public sealed class ProjectScopedEntityTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Run_RoundTrips_WithMachineAssignment_AndActiveQuery()
    {
        Guid projectId, machineId;
        using (var scope = _db.CreateScope())
        {
            projectId = await scope.SeedProjectAsync();
            machineId = await scope.SeedMachineAsync();
        }

        var run = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            MachineId = machineId,
            Status = RunStatus.Running,
            StartedAtUtc = DateTimeOffset.UtcNow,
            LastHeartbeatUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IRunRepository>().AddAsync(run);

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IRunRepository>();
        var loaded = await repo.GetByIdAsync(run.Id);

        Assert.NotNull(loaded);
        Assert.Equal(RunStatus.Running, loaded.Status);
        Assert.Equal(machineId, loaded.MachineId);

        var byProject = await repo.GetByProjectAsync(projectId);
        Assert.Contains(byProject, r => r.Id == run.Id);

        var active = await repo.GetActiveAsync();
        Assert.Contains(active, r => r.Id == run.Id);
    }

    [Fact]
    public async Task Run_MachineId_CanBeNull_ForPendingRun()
    {
        Guid projectId;
        using (var scope = _db.CreateScope())
            projectId = await scope.SeedProjectAsync();

        var run = new Run { Id = Guid.NewGuid(), ProjectId = projectId, Status = RunStatus.Pending };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IRunRepository>().AddAsync(run);

        using var verifyScope = _db.CreateScope();
        var loaded = await verifyScope.ServiceProvider.GetRequiredService<IRunRepository>().GetByIdAsync(run.Id);

        Assert.NotNull(loaded);
        Assert.Null(loaded.MachineId);
    }

    [Fact]
    public async Task Persona_RoundTrips_WithLlmConnection()
    {
        Guid projectId, llmConnectionId;
        using (var scope = _db.CreateScope())
        {
            projectId = await scope.SeedProjectAsync();
            llmConnectionId = await scope.SeedLlmConnectionAsync();
        }

        var persona = new Persona
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "Repair Persona",
            SystemPrompt = "You fix bugs.",
            PromptTemplate = "{{finding}}",
            LlmConnectionId = llmConnectionId,
            ExecutionMode = "Autonomous",
            AllowedToolsJson = """["run_tests"]""",
            AllowedConnectorIdsJson = "[]",
            AllowedWorkflowIdsJson = "[]",
            MaxIterations = 10,
            MaxLlmCostCents = 500,
            ApprovalPolicyJson = """{"requireApproval":false}""",
            AllowedSecretHandlesJson = "[]",
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IPersonaRepository>().AddAsync(persona);

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IPersonaRepository>();
        var loaded = await repo.GetByIdAsync(persona.Id);

        Assert.NotNull(loaded);
        Assert.Equal(llmConnectionId, loaded.LlmConnectionId);
        Assert.Equal(500, loaded.MaxLlmCostCents);

        var byProject = await repo.GetByProjectAsync(projectId);
        Assert.Contains(byProject, p => p.Id == persona.Id);
    }

    [Fact]
    public async Task Workflow_RoundTrips_WithFailurePolicy()
    {
        Guid projectId;
        using (var scope = _db.CreateScope())
            projectId = await scope.SeedProjectAsync();

        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "Godot Auto Repair",
            DefinitionJson = """{"steps":["RunTests","GenerateFindings"]}""",
            FailurePolicy = WorkflowFailurePolicy.ContinueOnError,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IWorkflowRepository>().AddAsync(workflow);

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IWorkflowRepository>();
        var loaded = await repo.GetByIdAsync(workflow.Id);

        Assert.NotNull(loaded);
        Assert.Equal(WorkflowFailurePolicy.ContinueOnError, loaded.FailurePolicy);

        var byProject = await repo.GetByProjectAsync(projectId);
        Assert.Contains(byProject, w => w.Id == workflow.Id);
    }

    [Fact]
    public async Task IntentPlan_RoundTrips_UnappliedByDefault()
    {
        Guid projectId;
        using (var scope = _db.CreateScope())
            projectId = await scope.SeedProjectAsync();

        var plan = new IntentPlan
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            NaturalLanguageRequest = "Deploy the weekly security scan",
            PlanningPath = IntentPlanningPath.Deterministic,
            ProposedWorkflowIdsJson = "[]",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IIntentPlanRepository>().AddAsync(plan);

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IIntentPlanRepository>();
        var loaded = await repo.GetByIdAsync(plan.Id);

        Assert.NotNull(loaded);
        Assert.Null(loaded.AppliedAtUtc);
        Assert.Equal(IntentPlanningPath.Deterministic, loaded.PlanningPath);

        var byProject = await repo.GetByProjectAsync(projectId);
        Assert.Contains(byProject, p => p.Id == plan.Id);
    }
}
