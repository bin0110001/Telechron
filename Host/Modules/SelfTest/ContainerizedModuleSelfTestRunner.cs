using System.Text.Json;
using Microsoft.Extensions.Logging;
using Telechron.Host.Agents.Dispatch;
using Telechron.Sdk.Agents;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Modules.SelfTest;

// R-SYS6: stages the module assembly as a Host blob and dispatches
// RunModuleSelfTest to the target Agent, which fetches it, runs it inside
// a container via IContainerExecutionService, and reports back through
// ICommandResultCorrelator -- module code never executes in the Host
// process (ModuleRuntime's ALC is lifecycle-only, R-MOD7).
public sealed class ContainerizedModuleSelfTestRunner(
    IArtifactBlobStore blobStore,
    IDispatchQueue dispatchQueue,
    ICommandResultCorrelator resultCorrelator,
    ILogger<ContainerizedModuleSelfTestRunner> logger) : IContainerizedModuleSelfTestRunner
{
    private static readonly TimeSpan SelfTestTimeout = TimeSpan.FromMinutes(5);

    public async Task<ModuleSelfTestResult> RunAsync(
        string moduleName, Guid machineId, string toolchainImageDigest, IReadOnlyList<string> declaredCapabilities,
        string moduleAssemblyPath, CancellationToken ct = default)
    {
        await using var fileStream = File.OpenRead(moduleAssemblyPath);
        var blobRef = await blobStore.SaveAsync(fileStream, Path.GetFileName(moduleAssemblyPath), ct);

        var commandId = Guid.NewGuid();
        var parametersJson = JsonSerializer.Serialize(new
        {
            moduleName,
            moduleAssemblyBlobRef = blobRef,
            toolchainImageDigest,
            maximallyRestricted = true,
            declaredCapabilities,
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

        logger.LogDebug("Containerized self-test for {ModuleName} completed (succeeded={Succeeded}).", moduleName, outcome.Succeeded);

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
