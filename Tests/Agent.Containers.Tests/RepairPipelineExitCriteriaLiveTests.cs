using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Host.Persistence.Tests.Phase3;
using Telechron.Host.Repair;
using Telechron.Modules.DotnetTestRunner;
using Telechron.Modules.DotnetToolchain;
using Telechron.Sdk.Containers;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Findings;
using Telechron.Sdk.Modules.Runners;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Repair;

namespace Telechron.Agent.Containers.Tests;

// Phase 7 exit criteria, live end-to-end: "a failing containerized test
// becomes a Code Finding, flows through the single pipeline, verifies in
// a container, respects the policy gate, and commits with a signed
// provenance record -- while a privileged-path or synthesis-requiring or
// oscillating fix is forced to human approval."
//
// Per this session's standing constraint, only the RequireApproval path
// is exercised live (never FullyAutonomous auto-commit) -- so this proves
// "verifies in a container + respects the policy gate" for the pause
// case; RepairPipelineOrchestratorTests already proves the Committed path
// end-to-end against a faked (non-container) Verify. The LLM fix-
// generation step itself is faked here (a hand-authored correct patch)
// rather than routed through a real Ollama call -- Phase 6 already proved
// the real Ollama path works; what THIS test proves is new to Phase 7:
// the real container Verify + real git apply/revert/commit + real
// governance/gates wired together as one pipeline.
public sealed class RepairPipelineExitCriteriaLiveTests : IAsyncLifetime
{
    private IDockerClient _dockerClient = null!;
    private bool _podmanAvailable;
    private string _repoDir = null!;
    private SqliteTestDatabase _db = null!;

    public async Task InitializeAsync()
    {
        _dockerClient = new DockerClientConfiguration(new Uri("npipe://./pipe/podman-machine-default")).CreateClient();
        _repoDir = Path.Combine(Path.GetTempPath(), "telechron-repair-exit-criteria-" + Guid.NewGuid());
        Directory.CreateDirectory(_repoDir);
        _db = new SqliteTestDatabase();

        try
        {
            await _dockerClient.System.PingAsync();
            _podmanAvailable = true;
        }
        catch
        {
            _podmanAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        _dockerClient.Dispose();
        await _db.DisposeAsync();
        if (Directory.Exists(_repoDir))
        {
            foreach (var file in Directory.GetFiles(_repoDir, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_repoDir, recursive: true);
        }
    }

    private sealed class NoOpGpuStateSanitizer : IGpuStateSanitizer
    {
        public Task SanitizeAsync(IReadOnlyList<string> gpuDeviceIds, CancellationToken ct = default) => Task.CompletedTask;
    }

    private PodmanContainerExecutionService CreateExecutionService() =>
        new(_dockerClient,
            new ImageProvenanceVerifier(Options.Create(new RegistryAllowlist())),
            Options.Create(new GpuTenancyPolicy()),
            new UnimplementedGpuCapabilityGate(),
            new NoOpGpuStateSanitizer(),
            new PassthroughWarmContainerPool(),
            NullLogger<PodmanContainerExecutionService>.Instance);

    private const string CsprojContent = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net9.0</TargetFramework>
            <Nullable>enable</Nullable>
            <IsPackable>false</IsPackable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
            <PackageReference Include="xunit" Version="2.9.0" />
            <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
          </ItemGroup>
        </Project>
        """;

    private const string FailingTestSource = """
        using Xunit;

        public class CalculatorTests
        {
            [Fact]
            public void Add_ReturnsSum()
            {
                Assert.Equal(4, Calculator.Add(2, 2));
            }
        }
        """;

    private const string BrokenCalculatorSource = """
        public static class Calculator
        {
            public static int Add(int a, int b) => a - b; // bug: should be a + b
        }
        """;

    [SkippableFact]
    public async Task RepairPipeline_FailingContainerizedTest_BecomesFindingFlowsThroughPipeline_VerifiesInContainer_PausesForApproval()
    {
        Skip.IfNot(_podmanAvailable, "Podman machine not reachable at npipe://./pipe/podman-machine-default");

        await File.WriteAllTextAsync(Path.Combine(_repoDir, "SampleTests.csproj"), CsprojContent);
        await File.WriteAllTextAsync(Path.Combine(_repoDir, "CalculatorTests.cs"), FailingTestSource);
        await File.WriteAllTextAsync(Path.Combine(_repoDir, "Calculator.cs"), BrokenCalculatorSource);

        var toolchain = new DotnetToolchainModule();
        var testRunner = new DotnetTestRunnerModule();
        var executionService = CreateExecutionService();

        // Step 1: run the REAL failing test in a REAL container (the
        // "Run" whose failure will become a Finding) -- same mechanism
        // Phase 6's ToolchainContainerRunLiveTests proved, reused here.
        var initialRun = await executionService.ExecuteAsync(new ContainerExecutionRequest(
            ImageDigest: toolchain.ToolchainImageDigest,
            Command: ["/bin/sh", "-c", $"cd /workspace && {toolchain.TestCommand} --logger \"console;verbosity=normal\""],
            WorkingDirectoryHostPath: _repoDir,
            ResourceLimits: new ContainerResourceLimits(MemoryBytes: 1024L * 1024 * 1024, CpuCores: 1.0, DiskBytes: 0),
            NetworkPolicy: new NetworkPolicy(true, []),
            RequiresGpu: false,
            Timeout: TimeSpan.FromMinutes(5)));
        Assert.Equal(ContainerExecutionOutcome.Completed, initialRun.Outcome);

        var initialTestResult = testRunner.ParseTestOutput(initialRun.StdOut, initialRun.StdErr, initialRun.ExitCode);
        Assert.False(initialTestResult.Succeeded, $"Expected the seeded bug to fail. StdOut: {initialRun.StdOut}");

        // Step 2: real Findings generation (R-FIX1/R-FIX8) from that real
        // failed Run.
        using var scope = _db.CreateScope();
        var projectId = await scope.SeedProjectAsync();
        var runId = await scope.SeedRunAsync(projectId);

        var findingsGenerator = new FindingsGenerator(new FailureClassifier());
        var run = new Run
        {
            Id = runId,
            ProjectId = projectId,
            Status = RunStatus.Failed,
            SuiteResultsJson = System.Text.Json.JsonSerializer.Serialize(initialTestResult),
        };
        var classificationInput = new FailureClassificationInput(RunStatus.Failed, null, false, initialRun.StdOut);
        var findings = findingsGenerator.GenerateFromRun(run, classificationInput);

        Assert.Single(findings);
        Assert.Equal(FindingFailureClass.Code, findings[0].FailureClass);

        var findingRepository = scope.ServiceProvider.GetRequiredService<IFindingRepository>();
        await findingRepository.AddAsync(findings[0]);

        // Step 3: the real pipeline. Generate Fix is faked with a
        // hand-authored correct patch (see class remarks) -- everything
        // downstream (Apply, real container Verify, gates, RequireApproval
        // policy, persistence, provenance) is real.
        var fixPatch = new PatchDiff([
            new PatchFileChange("Calculator.cs",
                "--- a/Calculator.cs\n+++ b/Calculator.cs\n@@ -1,3 +1,3 @@\n public static class Calculator\n {\n-    public static int Add(int a, int b) => a - b; // bug: should be a + b\n+    public static int Add(int a, int b) => a + b;\n }\n"),
        ]);

        var orchestrator = new RepairPipelineOrchestrator(
            versionControl: new GitRepairVersionControl(NullLogger<GitRepairVersionControl>.Instance),
            governor: new RepairAttemptGovernor(scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>()),
            concurrencyGate: new RepairConcurrencyGate(),
            deterministicFixProvider: new CompositeDeterministicFixProvider(),
            llmFixGenerator: new StaticLlmFixGenerator(fixPatch),
            verifier: new ContainerRepairVerifier(executionService, toolchain, testRunner),
            privilegedPathGuard: new PrivilegedPathGuard(),
            diffScopeGuard: new RepairDiffScopeGuard(),
            oscillationDetector: new OscillationDetector(),
            driftDetector: new NoDriftDetector(),
            provenanceSigner: new RepairProvenanceSigner(Options.Create(new RepairProvenanceSignerOptions())),
            repairAttemptRepository: scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>());

        var request = new RepairRequest(
            ProjectId: projectId,
            ProjectRootPath: _repoDir,
            ProjectPolicy: RepairPolicy.RequireApproval,
            Findings: findings,
            ActiveRequirements: [],
            DesignDocument: null);

        var outcome = await orchestrator.RunAsync(request);

        // "Verifies in a container": the fix was actually applied to disk
        // and actually re-tested inside a real container before this
        // status was reached.
        Assert.Equal(RepairOutcomeStatus.PendingApproval, outcome.Status);
        Assert.Contains("RequireApproval", outcome.Reason);

        var storedAttempt = await scope.ServiceProvider.GetRequiredService<IRepairAttemptRepository>().GetByIdAsync(outcome.RepairAttemptId!.Value);
        Assert.NotNull(storedAttempt);
        Assert.NotNull(storedAttempt.VerifyResultJson);
        var storedVerifyResult = System.Text.Json.JsonSerializer.Deserialize<TestRunResult>(storedAttempt.VerifyResultJson);
        Assert.True(storedVerifyResult!.Succeeded, "The applied patch should have made the real containerized test suite pass.");

        // Not yet committed -- RequireApproval pauses before Commit.
        Assert.Null(storedAttempt.CommitReference);

        // Working tree left as-applied (fixed) for human review.
        var finalSource = await File.ReadAllTextAsync(Path.Combine(_repoDir, "Calculator.cs"));
        Assert.Contains("a + b", finalSource);
    }

    private sealed class StaticLlmFixGenerator(PatchDiff patch) : ILlmFixGenerator
    {
        public Task<LlmFixResult> GenerateAsync(LlmFixContext context, CancellationToken ct = default) =>
            Task.FromResult(new LlmFixResult(true, patch, false, "static test fix"));
    }

    private sealed class NoDriftDetector : IArchitecturalDriftDetector
    {
        public Task<DriftCheckResult> CheckAsync(PatchDiff patch, IReadOnlyList<Requirement> activeRequirements, CancellationToken ct = default) =>
            Task.FromResult(new DriftCheckResult(false, null));
    }
}
