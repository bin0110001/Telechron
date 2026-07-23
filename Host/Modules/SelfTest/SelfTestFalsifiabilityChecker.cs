using System.Text.Json;
using Microsoft.Extensions.Logging;
using Telechron.Host.Agents.Dispatch;
using Telechron.Sdk.Agents;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Modules.SelfTest;

// R-MOD4a: dispatches the module's self-test into a container on a
// target Agent (R-SYS6 -- self-tests never run in-process on the Host;
// ModuleRuntime's ALC is lifecycle-only per R-MOD7) for both the pre-patch
// and post-patch assembly, and confirms the pre-patch run fails before
// trusting the post-patch pass.
public sealed class SelfTestFalsifiabilityChecker(
    IArtifactBlobStore blobStore,
    IDispatchQueue dispatchQueue,
    ICommandResultCorrelator resultCorrelator,
    ILogger<SelfTestFalsifiabilityChecker> logger) : ISelfTestFalsifiabilityChecker
{
    private static readonly TimeSpan SelfTestTimeout = TimeSpan.FromMinutes(5);

    public async Task<FalsifiabilityCheckResult> CheckAsync(
        string moduleName, Guid machineId, string toolchainImageDigest,
        string preSnapshotModuleAssemblyPath, string postSnapshotModuleAssemblyPath, CancellationToken ct = default)
    {
        if (!File.Exists(preSnapshotModuleAssemblyPath))
        {
            // Brand-new capability -- there is no "pre" to falsify against.
            // Falsifiability is vacuously satisfied; the negative control
            // burden shifts entirely to whether the post self-test is
            // itself non-trivial (a later, capability-specific concern).
            return new FalsifiabilityCheckResult(IsFalsifiable: true,
                "No pre-patch snapshot exists (new capability) -- falsifiability check is not applicable.");
        }

        var preResult = await RunContainerizedSelfTestAsync(
            moduleName, machineId, toolchainImageDigest, preSnapshotModuleAssemblyPath, "pre-patch", ct);
        if (preResult.Passed)
        {
            logger.LogWarning(
                "Self-test falsifiability check FAILED: pre-patch snapshot's self-test also passes ({Summary}) -- not a negative control (R-MOD4a).",
                preResult.Summary);
            return new FalsifiabilityCheckResult(IsFalsifiable: false,
                "Self-test passes against the pre-patch snapshot too -- it cannot distinguish broken code from fixed code (R-MOD4a).");
        }

        var postResult = await RunContainerizedSelfTestAsync(
            moduleName, machineId, toolchainImageDigest, postSnapshotModuleAssemblyPath, "post-patch", ct);
        if (!postResult.Passed)
        {
            return new FalsifiabilityCheckResult(IsFalsifiable: false,
                $"Post-patch self-test still fails: {postResult.Summary}");
        }

        return new FalsifiabilityCheckResult(IsFalsifiable: true,
            "Self-test fails on the pre-patch snapshot and passes on the post-patch snapshot -- valid negative control.");
    }

    private async Task<ModuleSelfTestResult> RunContainerizedSelfTestAsync(
        string moduleName, Guid machineId, string toolchainImageDigest, string assemblyPath, string label, CancellationToken ct)
    {
        await using var fileStream = File.OpenRead(assemblyPath);
        var blobRef = await blobStore.SaveAsync(fileStream, Path.GetFileName(assemblyPath), ct);

        var commandId = Guid.NewGuid();
        var parametersJson = JsonSerializer.Serialize(new
        {
            moduleName,
            moduleAssemblyBlobRef = blobRef,
            toolchainImageDigest,
            maximallyRestricted = true,
        });

        var outcome = await resultCorrelator.AwaitResultAsync(
            commandId,
            dispatch: () =>
            {
                var validation = dispatchQueue.Enqueue(machineId, new DispatchedCommand(
                    commandId, RunId: Guid.Empty, CommandKinds.RunModuleSelfTest, parametersJson, toolchainImageDigest));
                if (!validation.IsValid)
                    throw new InvalidOperationException($"Self-test dispatch rejected: {string.Join("; ", validation.Errors)}");
                return Task.CompletedTask;
            },
            SelfTestTimeout, ct);

        logger.LogDebug("Falsifiability check: {Label} self-test for {ModuleName} completed (succeeded={Succeeded}).",
            label, moduleName, outcome.Succeeded);

        // The Agent's RunModuleSelfTestCommandHandler already reduces the
        // harness's structured result down to CommandOutcome's
        // succeeded/summary/error shape -- reconstruct the same
        // ModuleSelfTestResult shape here rather than inventing a third
        // representation.
        return outcome.Succeeded
            ? ModuleSelfTestResult.Success(outcome.OutputSummary)
            : ModuleSelfTestResult.Failure(outcome.OutputSummary, outcome.ErrorMessage);
    }
}
