using System.Text.Json;
using Microsoft.Extensions.Logging;
using Telechron.Agent.Containers;
using Telechron.Sdk.Agents;
using Telechron.Sdk.Containers;
using Telechron.Sdk.Grpc;

namespace Telechron.Agent.Dispatch;

// R-MOD4/R-MOD5b: handles a RunModuleSelfTest dispatch end to end --
// fetches the module assembly from the Host (ArtifactFetcher), stages it
// alongside the pre-published self-test harness in a workspace directory,
// runs the harness inside a container via IContainerExecutionService
// (R-SYS6 -- module code never executes directly in the Agent process),
// and parses the harness's JSON stdout back into a result.
public sealed class RunModuleSelfTestCommandHandler(
    ArtifactFetcher artifactFetcher,
    IContainerExecutionService containerExecutionService,
    ModuleSelfTestHarnessLocator harnessLocator,
    ILogger<RunModuleSelfTestCommandHandler> logger) : ICommandHandler
{
    public string CommandKind => CommandKinds.RunModuleSelfTest;

    public async Task<CommandHandlerResult> HandleAsync(
        CommandDispatch command, AgentService.AgentServiceClient client, string machineId, string sessionToken,
        CancellationToken ct = default)
    {
        var parameters = JsonDocument.Parse(command.Parameters.ToString()).RootElement;
        var moduleName = parameters.GetProperty("moduleName").GetString()!;
        var blobRef = parameters.GetProperty("moduleAssemblyBlobRef").GetString()!;
        var toolchainImageDigest = parameters.GetProperty("toolchainImageDigest").GetString()!;
        var maximallyRestricted = parameters.TryGetProperty("maximallyRestricted", out var restrictedProp) && restrictedProp.GetBoolean();

        var workspaceDir = Path.Combine(Path.GetTempPath(), "telechron-module-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceDir);
        try
        {
            var fetchedAssemblyPath = await artifactFetcher.FetchToTempFileAsync(client, machineId, sessionToken, blobRef, ct);
            var moduleAssemblyWorkspacePath = Path.Combine(workspaceDir, "module.dll");
            File.Copy(fetchedAssemblyPath, moduleAssemblyWorkspacePath, overwrite: true);
            File.Delete(fetchedAssemblyPath);

            harnessLocator.CopyHarnessInto(workspaceDir);

            // R-MOD5b: a newly-installed/updated module's pre-trust run is
            // maximally restricted (network-denied, no capabilities beyond
            // what the self-test itself needs) -- an already-trusted
            // module's routine self-test re-run (R-MOD4) can use the
            // module's normal declared resource envelope instead.
            var resourceLimits = maximallyRestricted
                ? new ContainerResourceLimits(MemoryBytes: 256 * 1024 * 1024, CpuCores: 0.5, DiskBytes: 0)
                : new ContainerResourceLimits(MemoryBytes: 512 * 1024 * 1024, CpuCores: 1.0, DiskBytes: 0);

            var result = await containerExecutionService.ExecuteAsync(new ContainerExecutionRequest(
                ImageDigest: toolchainImageDigest,
                Command: ["dotnet", "/workspace/harness/Telechron.Tools.ModuleSelfTestHarness.dll", "/workspace/module.dll"],
                WorkingDirectoryHostPath: workspaceDir,
                ResourceLimits: resourceLimits,
                // R-MOD5b: network-denied regardless of the module's
                // declared capabilities -- pre-trust sandboxing means the
                // self-test itself never gets InternetAccess even if the
                // module will eventually be granted it.
                NetworkPolicy: NetworkPolicy.None,
                RequiresGpu: false,
                Timeout: TimeSpan.FromMinutes(5)), ct);

            return Interpret(moduleName, result);
        }
        finally
        {
            try { Directory.Delete(workspaceDir, recursive: true); }
            catch (IOException ex) { logger.LogWarning(ex, "Failed to clean up self-test workspace {WorkspaceDir}.", workspaceDir); }
        }
    }

    private CommandHandlerResult Interpret(string moduleName, ContainerExecutionResult containerResult)
    {
        if (containerResult.Outcome != ContainerExecutionOutcome.Completed)
        {
            return CommandHandlerResult.Failure(
                $"Self-test container for module '{moduleName}' did not complete cleanly: {containerResult.Outcome}. {containerResult.StdErr}");
        }

        try
        {
            var lastLine = containerResult.StdOut.Trim().Split('\n').LastOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? string.Empty;
            var selfTestResult = JsonSerializer.Deserialize<Sdk.Modules.ModuleSelfTestResult>(lastLine)
                ?? throw new JsonException("Harness produced no parseable result line.");

            return selfTestResult.Passed
                ? CommandHandlerResult.Success(selfTestResult.Summary)
                : CommandHandlerResult.Failure($"{selfTestResult.Summary}: {string.Join("; ", selfTestResult.Errors)}");
        }
        catch (JsonException ex)
        {
            return CommandHandlerResult.Failure($"Could not parse self-test harness output: {ex.Message}. StdOut: {containerResult.StdOut}");
        }
    }
}
