using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Telechron.Agent.Containers;
using Telechron.Sdk.Agents;
using Telechron.Sdk.Containers;

namespace Telechron.Agent.Dispatch;

// R-FIX2/R-SYS6: the Agent-side half of the repair pipeline's Verify
// stage. Fetches the zipped repair working-tree snapshot the Host staged
// (DispatchedRepairVerifier), unpacks it into a workspace directory, and
// runs the Project's Toolchain TestCommand inside a container -- the
// exact "run the command, hand output to the matching runner" split
// Phase 6 established, just executed on the Agent instead of assumed
// in-process on the Host (which has no IContainerExecutionService).
public sealed class RunRepairVerifyCommandHandler(
    ArtifactFetcher artifactFetcher,
    IContainerExecutionService containerExecutionService,
    ILogger<RunRepairVerifyCommandHandler> logger) : ICommandHandler
{
    private static readonly ContainerResourceLimits VerifyResourceLimits = new(MemoryBytes: 1024L * 1024 * 1024, CpuCores: 1.0, DiskBytes: 0);
    private static readonly NetworkPolicy VerifyNetworkPolicy = new(true, []);

    public string CommandKind => CommandKinds.RunRepairVerify;

    public async Task<CommandHandlerResult> HandleAsync(
        Telechron.Sdk.Grpc.CommandDispatch command, Telechron.Sdk.Grpc.AgentService.AgentServiceClient client, string machineId, string sessionToken,
        CancellationToken ct = default)
    {
        var parameters = JsonDocument.Parse(command.Parameters.ToString()).RootElement;
        var blobRef = parameters.GetProperty("workspaceBlobRef").GetString()!;
        var toolchainImageDigest = parameters.GetProperty("toolchainImageDigest").GetString()!;
        var testCommand = parameters.GetProperty("testCommand").GetString()!;

        var workspaceDir = Path.Combine(Path.GetTempPath(), "telechron-repair-verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceDir);
        try
        {
            var fetchedZipPath = await artifactFetcher.FetchToTempFileAsync(client, machineId, sessionToken, blobRef, ct);
            try
            {
                ZipFile.ExtractToDirectory(fetchedZipPath, workspaceDir, overwriteFiles: true);
            }
            finally
            {
                File.Delete(fetchedZipPath);
            }

            // R-FIX2 always runs Verify in a container (R-SYS6); network is
            // needed for package restore, matching ContainerRepairVerifier's
            // in-process default before this dispatch path existed.
            var result = await containerExecutionService.ExecuteAsync(new ContainerExecutionRequest(
                ImageDigest: toolchainImageDigest,
                Command: ["/bin/sh", "-c", $"cd /workspace && {testCommand}"],
                WorkingDirectoryHostPath: workspaceDir,
                ResourceLimits: VerifyResourceLimits,
                NetworkPolicy: VerifyNetworkPolicy,
                RequiresGpu: false,
                Timeout: TimeSpan.FromMinutes(10)), ct);

            return Interpret(result);
        }
        finally
        {
            try { Directory.Delete(workspaceDir, recursive: true); }
            catch (IOException ex) { logger.LogWarning(ex, "Failed to clean up repair verify workspace {WorkspaceDir}.", workspaceDir); }
        }
    }

    private static CommandHandlerResult Interpret(ContainerExecutionResult containerResult)
    {
        if (containerResult.Outcome != ContainerExecutionOutcome.Completed)
        {
            return CommandHandlerResult.Failure(
                $"Verify container did not complete cleanly: {containerResult.Outcome}. {containerResult.StdErr}");
        }

        // Success/failure of the TEST RUN itself (as opposed to the
        // container completing at all) is the Host's job to determine via
        // ITestRunnerModule.ParseTestOutput against the raw output -- this
        // handler reports what happened, not whether tests passed, exactly
        // as RunModuleSelfTestCommandHandler defers self-test-result
        // parsing semantics to its caller's JSON contract.
        return containerResult.ExitCode == 0
            ? CommandHandlerResult.Success(containerResult.StdOut)
            : CommandHandlerResult.Failure(containerResult.StdOut + "\n" + containerResult.StdErr);
    }
}
