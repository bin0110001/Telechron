namespace Telechron.Host.Reliability.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Telechron.Host.DesignDocs;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Host.Persistence.Tests.Phase3;
using Telechron.Host.Reliability;
using Telechron.Host.Repair;
using Telechron.Host.Workflows.Approvals;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Repair;

public sealed class HostSentinelTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

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

    // The real per-Project resolution (Toolchain/TestRunner/LlmConnection
    // via IModuleRuntime) is proven separately by RepairPipelineFactoryTests
    // -- this fake proves HostSentinel actually calls through
    // IRepairPipelineFactory and drives whatever orchestrator it returns,
    // rather than fabricating its own report (the bug this replaces).
    private sealed class FakeRepairPipelineFactory(RepairPipelineOrchestrator orchestrator) : IRepairPipelineFactory
    {
        public Guid? RequestedProjectId { get; private set; }
        public Guid? RequestedMachineId { get; private set; }

        public Task<RepairPipelineOrchestrator> CreateForProjectAsync(Guid projectId, Guid machineId, CancellationToken ct = default)
        {
            RequestedProjectId = projectId;
            RequestedMachineId = machineId;
            return Task.FromResult(orchestrator);
        }
    }

    private static RepairPipelineOrchestrator BuildRealOrchestrator(IServiceProvider services, PatchDiff patch) => new(
        versionControl: new GitRepairVersionControl(NullLogger<GitRepairVersionControl>.Instance),
        governor: new RepairAttemptGovernor(services.GetRequiredService<IRepairAttemptRepository>()),
        concurrencyGate: new RepairConcurrencyGate(),
        deterministicFixProvider: new CompositeDeterministicFixProvider(),
        llmFixGenerator: Telechron.Host.Repair.Tests.Fixtures.FakeLlmFixGenerator.ProducingPatch(patch),
        verifier: Telechron.Host.Repair.Tests.Fixtures.FakeRepairVerifier.Succeeding(),
        privilegedPathGuard: new PrivilegedPathGuard(),
        diffScopeGuard: new RepairDiffScopeGuard(),
        oscillationDetector: new OscillationDetector(),
        driftDetector: new Telechron.Host.Repair.Tests.Fixtures.FakeArchitecturalDriftDetector(),
        provenanceSigner: new RepairProvenanceSigner(Microsoft.Extensions.Options.Options.Create(new RepairProvenanceSignerOptions())),
        repairAttemptRepository: services.GetRequiredService<IRepairAttemptRepository>());

    [Fact]
    public async Task RunSelfRepairCheckAsync_NoOpenFindings_ReportsNoRepairNeeded()
    {
        using var scope = _db.CreateScope();
        var reflexiveWire = new ReflexiveDesignDocWire(new InMemoryDesignDocumentRepository());
        var fakeFactory = new FakeRepairPipelineFactory(
            BuildRealOrchestrator(scope.ServiceProvider, new PatchDiff([])));

        var sentinel = new HostSentinel(
            reflexiveWire, fakeFactory,
            scope.ServiceProvider.GetRequiredService<IFindingRepository>(),
            scope.ServiceProvider.GetRequiredService<IRequirementRepository>(),
            scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>(),
            scope.ServiceProvider.GetRequiredService<IMachineRepository>());

        var report = await sentinel.RunSelfRepairCheckAsync();

        Assert.False(report.RepairNeeded);
        Assert.Null(report.RepairAttempt);
        Assert.Null(fakeFactory.RequestedProjectId);
    }

    [Fact]
    public async Task RunSelfRepairCheckAsync_OpenCodeFinding_CallsRealOrchestratorViaFactory_AlwaysRequiresApproval()
    {
        using var scope = _db.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var owner = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Reflexive System User",
            Email = $"{Guid.NewGuid():N}@telechron.dev",
            AuthCredentialHash = "hash:placeholder",
            Role = Role.Admin,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await users.AddAsync(owner);

        var projectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await projectRepo.AddAsync(new Project
        {
            Id = ReflexiveDesignDocWire.TelechronSelfProjectId,
            Name = "Telechron",
            RootPath = ".",
            OwnerUserId = owner.Id,
            RepairPolicy = RepairPolicy.RequireApproval,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        var findingRepo = scope.ServiceProvider.GetRequiredService<IFindingRepository>();
        await findingRepo.AddAsync(new Finding
        {
            Id = Guid.NewGuid(),
            ProjectId = ReflexiveDesignDocWire.TelechronSelfProjectId,
            OriginFilePath = "Host/Skeleton.cs",
            RootCauseSignature = "sig-host-skeleton-selfrepair",
            Severity = FindingSeverity.Error,
            Category = "HostSelfRepair",
            FailureClass = FindingFailureClass.Code,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        var machineRepo = scope.ServiceProvider.GetRequiredService<IMachineRepository>();
        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "test-machine",
            Hostname = "test-host",
            MachineFingerprint = Guid.NewGuid().ToString("N"),
            RegisteredAtUtc = DateTimeOffset.UtcNow,
            IsOnline = true,
        };
        await machineRepo.AddAsync(machine);

        var reflexiveWire = new ReflexiveDesignDocWire(new InMemoryDesignDocumentRepository());
        var patch = new PatchDiff([new PatchFileChange("Host/Skeleton.cs", "--- a\n+++ b\n")]);
        var fakeFactory = new FakeRepairPipelineFactory(BuildRealOrchestrator(scope.ServiceProvider, patch));

        var sentinel = new HostSentinel(
            reflexiveWire, fakeFactory,
            findingRepo,
            scope.ServiceProvider.GetRequiredService<IRequirementRepository>(),
            scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>(),
            machineRepo);

        var report = await sentinel.RunSelfRepairCheckAsync();

        Assert.True(report.RepairNeeded);
        Assert.True(report.RequiresHumanApproval);
        Assert.Equal(ReflexiveDesignDocWire.TelechronSelfProjectId, fakeFactory.RequestedProjectId);
        Assert.Equal(machine.Id, fakeFactory.RequestedMachineId);
        // Host/Skeleton.cs matches the privileged-path guard (R-SEC4) --
        // real gate logic, not a fabricated report, forces PendingApproval.
        Assert.NotNull(report.RepairAttempt);
        Assert.Null(report.RepairAttempt!.ApprovalDecision);
    }
}
