namespace Telechron.Host.Repair;

using Microsoft.Extensions.Logging;
using Telechron.Host.Agents.Dispatch;
using Telechron.Host.Llm;
using Telechron.Host.Modules.Runtime;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules.Runners;
using Telechron.Sdk.Modules.Toolchains;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Repair;

// R-ENG4: the one place that resolves a Project's actual Toolchain/
// TestRunner/LlmConnection (via the Phase 5 module runtime and Phase 6
// LLM registry) and builds a RepairPipelineOrchestrator wired to them.
// Everything else about the orchestrator's construction (concurrency
// gate, governor, guards, provenance signer) is Project-independent and
// resolved once from DI; only these three pieces genuinely vary per
// Project being repaired.
public sealed class RepairPipelineFactory(
    IProjectRepository projectRepository,
    IToolchainRepository toolchainRepository,
    ILlmConnectionRepository llmConnectionRepository,
    IModuleRepository moduleRepository,
    IModuleRuntime moduleRuntime,
    IArtifactBlobStore blobStore,
    IDispatchQueue dispatchQueue,
    ICommandResultCorrelator resultCorrelator,
    ILlmDispatcher llmDispatcher,
    IRepairVersionControl versionControl,
    IRepairAttemptGovernor governor,
    IRepairConcurrencyGate concurrencyGate,
    IDeterministicFixProvider deterministicFixProvider,
    IPrivilegedPathGuard privilegedPathGuard,
    IRepairDiffScopeGuard diffScopeGuard,
    IOscillationDetector oscillationDetector,
    IRepairProvenanceSigner provenanceSigner,
    IRepairAttemptRepository repairAttemptRepository,
    ILoggerFactory loggerFactory,
    ILogger<RepairPipelineFactory> logger) : IRepairPipelineFactory
{
    public async Task<RepairPipelineOrchestrator> CreateForProjectAsync(Guid projectId, Guid machineId, CancellationToken ct = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, ct)
            ?? throw new InvalidOperationException($"Project '{projectId}' does not exist.");

        if (project.ToolchainId is not { } toolchainId)
            throw new InvalidOperationException($"Project '{projectId}' has no Toolchain assigned (Project.ToolchainId is null) -- cannot build a repair Verify stage.");
        if (project.LlmConnectionId is not { } llmConnectionId)
            throw new InvalidOperationException($"Project '{projectId}' has no LlmConnection assigned (Project.LlmConnectionId is null) -- cannot build Generate Fix / drift detection.");

        var toolchainRecord = await toolchainRepository.GetByIdAsync(toolchainId, ct)
            ?? throw new InvalidOperationException($"Toolchain '{toolchainId}' referenced by Project '{projectId}' no longer exists.");
        var toolchainModuleEntry = await moduleRepository.GetByIdAsync(toolchainRecord.ModuleId, ct)
            ?? throw new InvalidOperationException($"Module '{toolchainRecord.ModuleId}' backing Toolchain '{toolchainId}' no longer exists.");
        var toolchainModule = moduleRuntime.GetLoadedAs<IToolchainModule>(toolchainModuleEntry.Name)
            ?? throw new InvalidOperationException($"Toolchain module '{toolchainModuleEntry.Name}' is not currently loaded.");

        var testRunnerModule = await ResolveTestRunnerAsync(toolchainModule.Kind, ct)
            ?? throw new InvalidOperationException($"No loaded test-runner module supports Toolchain kind '{toolchainModule.Kind}'.");

        var llmConnection = await llmConnectionRepository.GetByIdAsync(llmConnectionId, ct)
            ?? throw new InvalidOperationException($"LlmConnection '{llmConnectionId}' referenced by Project '{projectId}' no longer exists.");

        var verifier = new DispatchedRepairVerifier(
            blobStore, dispatchQueue, resultCorrelator, machineId, toolchainModule, testRunnerModule,
            loggerFactory.CreateLogger<DispatchedRepairVerifier>());
        var llmFixGenerator = new LlmFixGenerator(llmDispatcher, llmConnection);
        var driftDetector = new ArchitecturalDriftDetector(llmDispatcher, llmConnection);

        logger.LogDebug(
            "Built RepairPipelineOrchestrator for Project '{ProjectId}' using Toolchain '{ToolchainName}' / TestRunner '{TestRunnerName}'.",
            projectId, toolchainModuleEntry.Name, testRunnerModule.Name);

        return new RepairPipelineOrchestrator(
            versionControl, governor, concurrencyGate, deterministicFixProvider, llmFixGenerator, verifier,
            privilegedPathGuard, diffScopeGuard, oscillationDetector, driftDetector, provenanceSigner, repairAttemptRepository);
    }

    private async Task<ITestRunnerModule?> ResolveTestRunnerAsync(string toolchainKind, CancellationToken ct)
    {
        var allModules = await moduleRepository.GetAllAsync(ct);
        foreach (var moduleRecord in allModules.Where(m => m.Kind == "runner"))
        {
            var runner = moduleRuntime.GetLoadedAs<ITestRunnerModule>(moduleRecord.Name);
            if (runner is not null && string.Equals(runner.SupportedToolchainKind, toolchainKind, StringComparison.OrdinalIgnoreCase))
                return runner;
        }
        return null;
    }
}
