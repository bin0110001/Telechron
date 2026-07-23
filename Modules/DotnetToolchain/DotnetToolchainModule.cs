using Telechron.Sdk.Modules;
using Telechron.Sdk.Modules.Toolchains;

namespace Telechron.Modules.DotnetToolchain;

// R-DM14/R-RUN5: the first end-to-end Toolchain module (.NET), matching
// containers/toolchains/dotnet/Dockerfile's pinned digest
// (containers/README.md). Pure descriptor -- no build/test/verify
// command actually runs here; per R-SYS6 that always happens inside an
// Agent container via the dispatch path, this module only says what
// command and what image that container should use.
public sealed class DotnetToolchainModule : IToolchainModule
{
    public string Name => "telechron.toolchain.dotnet";
    public string Kind => "dotnet";
    public ModuleVersion Version => new(1, 0, 0);
    public IReadOnlyList<string> DeclaredCapabilities => [];

    public string ToolchainImageDigest =>
        "mcr.microsoft.com/dotnet/sdk@sha256:72b2c1fba104eed0765e76c66256dd57b8b00c5e7c7fd16ad3eb254ad18db3fc";

    public string BuildCommand => "dotnet build";
    public string TestCommand => "dotnet test";
    public string VerifyCommand => "dotnet test";
    public string? ExportCommand => "dotnet publish -c Release";
    public string? DeployCommand => null;

    public IReadOnlyDictionary<string, string> EnvironmentRequirements => new Dictionary<string, string>
    {
        ["DOTNET_NOLOGO"] = "1",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
    };

    public Task<ModuleSelfTestResult> RunSelfTestAsync(CancellationToken ct = default)
    {
        // A descriptor module's self-test is a consistency check on its
        // own declared data, not an external command run -- the actual
        // BuildCommand/TestCommand only get exercised for real by the
        // R-SYS6 container path, which this module never touches itself.
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(BuildCommand)) errors.Add("BuildCommand is empty.");
        if (string.IsNullOrWhiteSpace(TestCommand)) errors.Add("TestCommand is empty.");
        if (string.IsNullOrWhiteSpace(VerifyCommand)) errors.Add("VerifyCommand is empty.");
        if (!ToolchainImageDigest.Contains("@sha256:")) errors.Add("ToolchainImageDigest is not a digest reference.");

        return Task.FromResult(errors.Count == 0
            ? ModuleSelfTestResult.Success("Toolchain descriptor is internally consistent.")
            : ModuleSelfTestResult.Failure("Toolchain descriptor is invalid.", [.. errors]));
    }
}
