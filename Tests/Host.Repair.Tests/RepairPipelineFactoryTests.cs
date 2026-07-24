using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Telechron.Host.Llm;
using Telechron.Host.Modules.Runtime;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Host.Persistence.Tests.Phase3;
using Telechron.Sdk.Containers;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Modules.Llm;
using Telechron.Sdk.Modules.Runners;
using Telechron.Sdk.Modules.Toolchains;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Repair;
using Telechron.Sdk.Security;

namespace Telechron.Host.Repair.Tests;

// R-ENG4: proves RepairPipelineFactory resolves a Project's REAL, seeded
// Toolchain/TestRunner/LlmConnection (not fixed globals) via SQLite +
// IModuleRuntime, rather than the previous state where nothing in the Host
// ever constructed a RepairPipelineOrchestrator at all.
public sealed class RepairPipelineFactoryTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private sealed class FakeToolchainModule : IToolchainModule
    {
        public string Name => "test.toolchain.fake";
        public string Kind => "fake-kind";
        public ModuleVersion Version => new(1, 0, 0);
        public IReadOnlyList<string> DeclaredCapabilities => [];
        public string ToolchainImageDigest => "registry.example/fake@sha256:" + new string('a', 64);
        public string BuildCommand => "build";
        public string TestCommand => "test";
        public string VerifyCommand => "verify";
        public string? ExportCommand => null;
        public string? DeployCommand => null;
        public IReadOnlyDictionary<string, string> EnvironmentRequirements => new Dictionary<string, string>();
        public Task<ModuleSelfTestResult> RunSelfTestAsync(CancellationToken ct = default) =>
            Task.FromResult(ModuleSelfTestResult.Success("n/a"));
    }

    private sealed class FakeTestRunnerModule : ITestRunnerModule
    {
        public string Name => "test.runner.fake";
        public string Kind => "runner";
        public ModuleVersion Version => new(1, 0, 0);
        public IReadOnlyList<string> DeclaredCapabilities => [];
        public string SupportedToolchainKind => "fake-kind";
        public TestRunResult ParseTestOutput(string stdOut, string stdErr, int? exitCode) =>
            new(true, [], [], "n/a");
        public Task<ModuleSelfTestResult> RunSelfTestAsync(CancellationToken ct = default) =>
            Task.FromResult(ModuleSelfTestResult.Success("n/a"));
    }

    private sealed class FakeModuleRuntime(IModule toolchainModule, IModule testRunnerModule) : IModuleRuntime
    {
        public Task<LoadedModule> LoadAsync(string moduleAssemblyPath, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public LoadedModule? GetLoaded(string moduleName) => null;

        public TModule? GetLoadedAs<TModule>(string moduleName) where TModule : class, IModule
        {
            IModule? candidate = moduleName switch
            {
                _ when moduleName == toolchainModule.Name => toolchainModule,
                _ when moduleName == testRunnerModule.Name => testRunnerModule,
                _ => null,
            };
            return candidate as TModule;
        }

        public Task<ModuleUnloadResult> UnloadAsync(string moduleName, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static IArtifactBlobStore NewBlobStore() =>
        new Telechron.Host.Persistence.FilesystemArtifactBlobStore(
            Path.Combine(Path.GetTempPath(), "telechron-repair-factory-tests-" + Guid.NewGuid().ToString("N")));

    private sealed class NullDispatchQueue : IDispatchQueue
    {
        public CommandValidationResult Enqueue(Guid machineId, DispatchedCommand command) =>
            throw new NotSupportedException("Not exercised by this test -- only factory resolution is under test.");

        public IAsyncEnumerable<DispatchedCommand> ReadAllAsync(Guid machineId, CancellationToken ct) =>
            throw new NotSupportedException("Not exercised by this test -- only factory resolution is under test.");
    }

    private sealed class NullCommandResultCorrelator : ICommandResultCorrelator
    {
        public Task<CommandOutcome> AwaitResultAsync(Guid commandId, Func<Task> dispatch, TimeSpan timeout, CancellationToken ct = default) =>
            throw new NotSupportedException("Not exercised by this test -- only factory resolution is under test.");

        public void Complete(CommandOutcome outcome) =>
            throw new NotSupportedException("Not exercised by this test -- only factory resolution is under test.");
    }

    private sealed class NullLlmDispatcher : ILlmDispatcher
    {
        public Task<LlmCompletionResult> DispatchAsync(LlmConnection connection, Guid? projectId, LlmCompletionRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException("Not exercised by this test -- only factory resolution is under test.");
    }

    [Fact]
    public async Task CreateForProjectAsync_ProjectWithRealToolchainAndLlmConnection_BuildsWorkingOrchestrator()
    {
        using var scope = _db.CreateScope();
        var toolchainModule = new FakeToolchainModule();
        var testRunnerModule = new FakeTestRunnerModule();

        var moduleRepo = scope.ServiceProvider.GetRequiredService<IModuleRepository>();
        var toolchainModuleRow = new Module
        {
            Id = Guid.NewGuid(), Name = toolchainModule.Name, Kind = "toolchain",
            VersionMajor = 1, VersionMinor = 0, VersionPatch = 0,
            CapabilitiesJson = "[]", TestCommand = "test", SourceCodeRef = "n/a",
            InstalledAtUtc = DateTimeOffset.UtcNow,
        };
        await moduleRepo.AddAsync(toolchainModuleRow);
        await moduleRepo.AddAsync(new Module
        {
            Id = Guid.NewGuid(), Name = testRunnerModule.Name, Kind = "runner",
            VersionMajor = 1, VersionMinor = 0, VersionPatch = 0,
            CapabilitiesJson = "[]", TestCommand = "test", SourceCodeRef = "n/a",
            InstalledAtUtc = DateTimeOffset.UtcNow,
        });

        var toolchainRepo = scope.ServiceProvider.GetRequiredService<IToolchainRepository>();
        var toolchainRow = new Toolchain
        {
            Id = Guid.NewGuid(), Name = "Fake Toolchain", ModuleId = toolchainModuleRow.Id,
            BuildCommand = "build", TestCommand = "test", VerifyCommand = "verify",
            EnvironmentRequirementsJson = "{}",
        };
        await toolchainRepo.AddAsync(toolchainRow);

        var llmConnectionRepo = scope.ServiceProvider.GetRequiredService<ILlmConnectionRepository>();
        var llmConnectionRow = new LlmConnection
        {
            Id = Guid.NewGuid(), Name = "Fake LLM", Provider = "fake",
            ConfigurationJson = "{}", CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await llmConnectionRepo.AddAsync(llmConnectionRow);

        var projectId = await scope.SeedProjectAsync();
        var projectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var project = (await projectRepo.GetByIdAsync(projectId))!;
        await projectRepo.UpdateAsync(new Project
        {
            Id = project.Id,
            Name = project.Name,
            RootPath = project.RootPath,
            OwnerUserId = project.OwnerUserId,
            RepairPolicy = project.RepairPolicy,
            ToolchainId = toolchainRow.Id,
            LlmConnectionId = llmConnectionRow.Id,
            CreatedAtUtc = project.CreatedAtUtc,
        });

        var factory = new RepairPipelineFactory(
            projectRepo, toolchainRepo, llmConnectionRepo, moduleRepo,
            new FakeModuleRuntime(toolchainModule, testRunnerModule),
            NewBlobStore(),
            new NullDispatchQueue(),
            new NullCommandResultCorrelator(),
            new NullLlmDispatcher(),
            new GitRepairVersionControl(NullLogger<GitRepairVersionControl>.Instance),
            new RepairAttemptGovernor(scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>()),
            new RepairConcurrencyGate(),
            new CompositeDeterministicFixProvider(),
            new PrivilegedPathGuard(),
            new RepairDiffScopeGuard(),
            new OscillationDetector(),
            new RepairProvenanceSigner(Microsoft.Extensions.Options.Options.Create(new RepairProvenanceSignerOptions())),
            scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>(),
            NullLoggerFactory.Instance,
            NullLogger<RepairPipelineFactory>.Instance);

        var orchestrator = await factory.CreateForProjectAsync(projectId, Guid.NewGuid());

        Assert.NotNull(orchestrator);
    }

    [Fact]
    public async Task CreateForProjectAsync_ProjectWithNoToolchainAssigned_Throws()
    {
        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();

        var factory = new RepairPipelineFactory(
            scope.ServiceProvider.GetRequiredService<IProjectRepository>(),
            scope.ServiceProvider.GetRequiredService<IToolchainRepository>(),
            scope.ServiceProvider.GetRequiredService<ILlmConnectionRepository>(),
            scope.ServiceProvider.GetRequiredService<IModuleRepository>(),
            new FakeModuleRuntime(new FakeToolchainModule(), new FakeTestRunnerModule()),
            NewBlobStore(),
            new NullDispatchQueue(),
            new NullCommandResultCorrelator(),
            new NullLlmDispatcher(),
            new GitRepairVersionControl(NullLogger<GitRepairVersionControl>.Instance),
            new RepairAttemptGovernor(scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>()),
            new RepairConcurrencyGate(),
            new CompositeDeterministicFixProvider(),
            new PrivilegedPathGuard(),
            new RepairDiffScopeGuard(),
            new OscillationDetector(),
            new RepairProvenanceSigner(Microsoft.Extensions.Options.Options.Create(new RepairProvenanceSignerOptions())),
            scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>(),
            NullLoggerFactory.Instance,
            NullLogger<RepairPipelineFactory>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateForProjectAsync(projectId, Guid.NewGuid()));
    }
}
