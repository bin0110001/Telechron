using System.IO.Compression;
using System.Text.Json;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Modules.Functions;

namespace Telechron.Modules.CoreFunctions;

// R-DM4/R-WF1/R-WF2: two representative Function kinds proving the
// in-process/container split IFunctionExecutorModule declares:
//  - "zip": a pure data transform (System.IO.Compression, no network, no
//    external process) -- safe to run in the Host's own process, and
//    RequiresContainer("zip") reports false accordingly.
//  - "git": touches the filesystem AND spawns an external process
//    (`git`) -- per R-SYS6 that must run in a container, so
//    RequiresContainer("git") reports true and this module only builds
//    the command line, never executes it.
public sealed class CoreFunctionsModule : IFunctionExecutorModule
{
    public string Name => "telechron.functions.core";
    public string Kind => "function-executor";
    public ModuleVersion Version => new(1, 0, 0);
    public IReadOnlyList<string> DeclaredCapabilities => ["FilesystemRead", "FilesystemWrite"];
    public IReadOnlyList<string> SupportedFunctionKinds => ["zip", "git"];

    public bool RequiresContainer(string functionKind) => functionKind switch
    {
        "zip" => false,
        "git" => true,
        _ => throw new ArgumentOutOfRangeException(nameof(functionKind), functionKind, "Unsupported function kind."),
    };

    public Task<FunctionInvocationResult> InvokeInProcessAsync(
        string functionKind, string inputArtifactTypesJson, string parametersJson, CancellationToken ct = default)
    {
        if (functionKind != "zip")
        {
            throw new InvalidOperationException(
                $"'{functionKind}' requires a container (RequiresContainer returned true) -- InvokeInProcessAsync must not be called for it.");
        }

        return Task.FromResult(InvokeZip(parametersJson));
    }

    private static FunctionInvocationResult InvokeZip(string parametersJson)
    {
        JsonElement parameters;
        try
        {
            parameters = JsonDocument.Parse(parametersJson).RootElement;
        }
        catch (JsonException ex)
        {
            return FunctionInvocationResult.Failure($"Invalid parameters JSON: {ex.Message}");
        }

        if (!parameters.TryGetProperty("sourceDirectory", out var sourceProp) ||
            !parameters.TryGetProperty("destinationZipPath", out var destProp))
        {
            return FunctionInvocationResult.Failure("'sourceDirectory' and 'destinationZipPath' are required parameters.");
        }

        var sourceDirectory = sourceProp.GetString()!;
        var destinationZipPath = destProp.GetString()!;

        // Defense in depth: parameters ultimately originate from a
        // Workflow definition, which is Project-authored but still
        // external input to this module -- reject traversal even though
        // this runs in-process rather than a container.
        if (sourceDirectory.Contains("..") || destinationZipPath.Contains(".."))
            return FunctionInvocationResult.Failure("Path traversal ('..') is not allowed in zip parameters.");

        if (!Directory.Exists(sourceDirectory))
            return FunctionInvocationResult.Failure($"Source directory does not exist: {sourceDirectory}");

        try
        {
            if (File.Exists(destinationZipPath))
                File.Delete(destinationZipPath);
            ZipFile.CreateFromDirectory(sourceDirectory, destinationZipPath);
        }
        catch (IOException ex)
        {
            return FunctionInvocationResult.Failure($"Zip creation failed: {ex.Message}");
        }

        return FunctionInvocationResult.Success(
            """["application/zip"]""", $"Created {destinationZipPath} from {sourceDirectory}.");
    }

    public IReadOnlyList<string> BuildContainerCommand(string functionKind, string inputArtifactTypesJson, string parametersJson)
    {
        if (functionKind != "git")
        {
            throw new InvalidOperationException(
                $"'{functionKind}' does not require a container (RequiresContainer returned false) -- use InvokeInProcessAsync instead.");
        }

        var parameters = JsonDocument.Parse(parametersJson).RootElement;
        var gitCommand = parameters.GetProperty("gitCommand").GetString()!;
        // A pattern-validated schema (CommandDispatchValidator, mirroring
        // the run-tests/build pattern from Phase 4) is what actually
        // enforces this is safe before dispatch -- this module only
        // describes the command, it doesn't validate or execute it.
        return ["git", .. gitCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries)];
    }

    public Task<ModuleSelfTestResult> RunSelfTestAsync(CancellationToken ct = default)
    {
        var errors = new List<string>();

        if (RequiresContainer("zip")) errors.Add("'zip' must not require a container.");
        if (!RequiresContainer("git")) errors.Add("'git' must require a container.");

        try
        {
            _ = RequiresContainer("unknown-kind");
            errors.Add("RequiresContainer('unknown-kind') should have thrown but did not.");
        }
        catch (ArgumentOutOfRangeException)
        {
            // expected
        }

        var command = BuildContainerCommand("git", "[]", """{"gitCommand": "clone https://example.invalid/repo.git"}""");
        if (command is not ["git", "clone", "https://example.invalid/repo.git"])
            errors.Add("BuildContainerCommand('git', ...) did not produce the expected argv.");

        return Task.FromResult(errors.Count == 0
            ? ModuleSelfTestResult.Success("In-process/container-required split and command-building are consistent.")
            : ModuleSelfTestResult.Failure("Function executor self-consistency check failed.", [.. errors]));
    }
}
