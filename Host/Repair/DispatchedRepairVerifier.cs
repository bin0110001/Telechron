using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Telechron.Host.Agents.Dispatch;
using Telechron.Sdk.Agents;
using Telechron.Sdk.Modules.Runners;
using Telechron.Sdk.Modules.Toolchains;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Repair;

namespace Telechron.Host.Repair;

// R-FIX2/R-SYS6: Verify always runs inside the Phase 4 container boundary,
// which only exists on the Agent -- IContainerExecutionService has no
// implementation in the Host process. This zips the repair working tree
// (projectRootPath, the git snapshot GitRepairVersionControl manages),
// ships it to the target Agent as one blob (the same FetchArtifact
// primitive Phase 5's self-test dispatch uses, just carrying a directory
// instead of a single assembly), and dispatches a RunRepairVerify command
// that runs build+test in a container there and reports back through the
// existing ICommandResultCorrelator -- no parallel correlation mechanism.
public sealed class DispatchedRepairVerifier(
    IArtifactBlobStore blobStore,
    IDispatchQueue dispatchQueue,
    ICommandResultCorrelator resultCorrelator,
    Guid machineId,
    IToolchainModule toolchain,
    ITestRunnerModule testRunner,
    ILogger<DispatchedRepairVerifier> logger) : IRepairVerifier
{
    private static readonly TimeSpan VerifyTimeout = TimeSpan.FromMinutes(10);

    public async Task<VerifyResult> VerifyAsync(string projectRootPath, CancellationToken ct = default)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), "telechron-repair-verify-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            ZipFile.CreateFromDirectory(projectRootPath, zipPath, CompressionLevel.Fastest, includeBaseDirectory: false);

            string blobRef;
            await using (var zipStream = File.OpenRead(zipPath))
            {
                blobRef = await blobStore.SaveAsync(zipStream, "repair-verify-workspace.zip", ct);
            }

            var commandId = Guid.NewGuid();
            var parametersJson = JsonSerializer.Serialize(new
            {
                workspaceBlobRef = blobRef,
                toolchainImageDigest = toolchain.ToolchainImageDigest,
                testCommand = toolchain.TestCommand,
                testRunnerKind = testRunner.SupportedToolchainKind,
                environmentRequirements = toolchain.EnvironmentRequirements,
            });

            var outcome = await resultCorrelator.AwaitResultAsync(
                commandId,
                dispatch: () =>
                {
                    var validation = dispatchQueue.Enqueue(machineId, new DispatchedCommand(
                        commandId, RunId: Guid.Empty, CommandKinds.RunRepairVerify, parametersJson, toolchain.ToolchainImageDigest));
                    if (!validation.IsValid)
                        throw new InvalidOperationException($"Repair verify dispatch rejected: {string.Join("; ", validation.Errors)}");
                    return Task.CompletedTask;
                },
                VerifyTimeout, ct);

            logger.LogDebug("Repair verify dispatch completed (succeeded={Succeeded}).", outcome.Succeeded);

            if (!outcome.Succeeded && string.IsNullOrWhiteSpace(outcome.OutputSummary))
            {
                // Container never produced parseable test output (crash,
                // timeout, image pull failure, etc.) -- distinct from "the
                // tests ran and failed," which IS parseable and handled below.
                return new VerifyResult(false, null, outcome.ErrorMessage);
            }

            var testRunResult = testRunner.ParseTestOutput(outcome.OutputSummary, outcome.ErrorMessage, exitCode: outcome.Succeeded ? 0 : 1);
            return new VerifyResult(testRunResult.Succeeded, testRunResult, outcome.OutputSummary);
        }
        finally
        {
            try { File.Delete(zipPath); }
            catch (IOException ex) { logger.LogDebug(ex, "Could not delete repair verify staging zip '{ZipPath}'.", zipPath); }
        }
    }
}
