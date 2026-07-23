using Microsoft.Extensions.DependencyInjection;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Tests.Phase3;

// Shared FK-seeding helpers for Phase 3 entity round-trip tests — Run,
// Persona, Workflow, etc. all ultimately need a real User+Project (and
// sometimes Machine/LlmConnection) to satisfy real foreign keys.
public static class Phase3Seeding
{
    public static async Task<Guid> SeedProjectAsync(this IServiceScope scope)
    {
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();

        var owner = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test Owner",
            Email = $"{Guid.NewGuid():N}@telechron.dev",
            AuthCredentialHash = "hash:placeholder",
            Role = Role.Admin,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await users.AddAsync(owner);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            RootPath = "/repo/test",
            OwnerUserId = owner.Id,
            RepairPolicy = RepairPolicy.RequireApproval,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await projects.AddAsync(project);

        return project.Id;
    }

    public static async Task<Guid> SeedUserAsync(this IServiceScope scope)
    {
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test User",
            Email = $"{Guid.NewGuid():N}@telechron.dev",
            AuthCredentialHash = "hash:placeholder",
            Role = Role.Operator,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await users.AddAsync(user);
        return user.Id;
    }

    public static async Task<Guid> SeedMachineAsync(this IServiceScope scope)
    {
        var machines = scope.ServiceProvider.GetRequiredService<IMachineRepository>();
        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Test Machine",
            Hostname = "test-machine.local",
            RegisteredAtUtc = DateTimeOffset.UtcNow,
            IsOnline = true,
        };
        await machines.AddAsync(machine);
        return machine.Id;
    }

    public static async Task<Guid> SeedLlmConnectionAsync(this IServiceScope scope)
    {
        var connections = scope.ServiceProvider.GetRequiredService<ILlmConnectionRepository>();
        var connection = new LlmConnection
        {
            Id = Guid.NewGuid(),
            Name = "Test LLM Connection",
            Provider = "Ollama",
            ConfigurationJson = """{"baseUrl":"http://localhost:11434"}""",
            SecretHandle = null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await connections.AddAsync(connection);
        return connection.Id;
    }

    public static async Task<Guid> SeedWorkflowAsync(this IServiceScope scope, Guid projectId)
    {
        var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowRepository>();
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "Test Workflow",
            DefinitionJson = """{"steps":[]}""",
            FailurePolicy = WorkflowFailurePolicy.FailFast,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await workflows.AddAsync(workflow);
        return workflow.Id;
    }

    public static async Task<Guid> SeedWorkflowRunAsync(this IServiceScope scope, Guid workflowId)
    {
        var workflowRuns = scope.ServiceProvider.GetRequiredService<IWorkflowRunRepository>();
        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            Status = WorkflowRunStatus.Pending,
            DefinitionSnapshotJson = """{"steps":[]}""",
        };
        await workflowRuns.AddAsync(run);
        return run.Id;
    }

    public static async Task<Guid> SeedRunAsync(this IServiceScope scope, Guid projectId)
    {
        var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var run = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Status = RunStatus.Pending,
        };
        await runs.AddAsync(run);
        return run.Id;
    }

    public static async Task<Guid> SeedFindingAsync(this IServiceScope scope, Guid projectId)
    {
        var findings = scope.ServiceProvider.GetRequiredService<IFindingRepository>();
        var finding = new Finding
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RootCauseSignature = "test-signature",
            Severity = FindingSeverity.Error,
            Category = "test failure",
            FailureClass = FindingFailureClass.Code,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await findings.AddAsync(finding);
        return finding.Id;
    }
}
