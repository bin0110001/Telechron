using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Telechron.Sdk.Agents;
using Telechron.Sdk.Containers;

namespace Telechron.Agent.Dispatch;

// R-BUILD5: fetches the zipped synthesis source bundle (module source,
// self-test source, a minimal .csproj, and the Host's own Telechron.Sdk.dll
// to compile against) the Host staged, runs `dotnet build` inside a
// container (R-SYS6 -- synthesized/untrusted code never compiles or runs
// in-process on either Host or Agent), then `dotnet test` against the
// self-test project, and uploads the resulting assembly back to the Host
// via StoreArtifact so IModuleTrustEvaluator can run its real pre-trust
// pipeline against a real compiled DLL.
public sealed class RunCapabilitySynthesisBuildCommandHandler(
    ArtifactFetcher artifactFetcher,
    ArtifactUploader artifactUploader,
    IContainerExecutionService containerExecutionService,
    ILogger<RunCapabilitySynthesisBuildCommandHandler> logger) : ICommandHandler
{
    private static readonly ContainerResourceLimits BuildResourceLimits = new(MemoryBytes: 1024L * 1024 * 1024, CpuCores: 1.0, DiskBytes: 0);
    private static readonly NetworkPolicy BuildNetworkPolicy = new(true, []);

    public string CommandKind => CommandKinds.RunCapabilitySynthesisBuild;

    public async Task<CommandHandlerResult> HandleAsync(
        Telechron.Sdk.Grpc.CommandDispatch command, Telechron.Sdk.Grpc.AgentService.AgentServiceClient client, string machineId, string sessionToken,
        CancellationToken ct = default)
    {
        var parameters = JsonDocument.Parse(command.Parameters.ToString()).RootElement;
        var sourceBundleBlobRef = parameters.GetProperty("sourceBundleBlobRef").GetString()!;
        var toolchainImageDigest = parameters.GetProperty("toolchainImageDigest").GetString()!;
        var moduleName = parameters.GetProperty("moduleName").GetString()!;

        var workspaceDir = Path.Combine(Path.GetTempPath(), "telechron-synthesis-build-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceDir);
        try
        {
            var fetchedZipPath = await artifactFetcher.FetchToTempFileAsync(client, machineId, sessionToken, sourceBundleBlobRef, ct);
            try
            {
                ZipFile.ExtractToDirectory(fetchedZipPath, workspaceDir, overwriteFiles: true);
            }
            finally
            {
                File.Delete(fetchedZipPath);
            }

            var buildResult = await containerExecutionService.ExecuteAsync(new ContainerExecutionRequest(
                ImageDigest: toolchainImageDigest,
                Command: ["/bin/sh", "-c",
                    $"cd /workspace && dotnet build {moduleName}.csproj -c Release && dotnet test SelfTest/{moduleName}.SelfTest.csproj"],
                WorkingDirectoryHostPath: workspaceDir,
                ResourceLimits: BuildResourceLimits,
                NetworkPolicy: BuildNetworkPolicy,
                RequiresGpu: false,
                Timeout: TimeSpan.FromMinutes(10)), ct);

            if (buildResult.Outcome != ContainerExecutionOutcome.Completed || buildResult.ExitCode != 0)
            {
                return CommandHandlerResult.Failure(
                    $"Synthesis build/self-test failed: {buildResult.Outcome}, exit={buildResult.ExitCode}. StdOut: {buildResult.StdOut}\nStdErr: {buildResult.StdErr}");
            }

            var builtAssemblyPath = Directory.EnumerateFiles(workspaceDir, $"{moduleName}.dll", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (builtAssemblyPath is null)
            {
                return CommandHandlerResult.Failure(
                    $"Build succeeded but no output assembly named '{moduleName}.dll' was found under the workspace.");
            }

            var blobRef = await artifactUploader.UploadFromFileAsync(
                client, machineId, sessionToken, builtAssemblyPath, $"{moduleName}.dll", ct);

            return CommandHandlerResult.Success(blobRef);
        }
        finally
        {
            try { Directory.Delete(workspaceDir, recursive: true); }
            catch (IOException ex) { logger.LogWarning(ex, "Failed to clean up synthesis build workspace {WorkspaceDir}.", workspaceDir); }
        }
    }
}
