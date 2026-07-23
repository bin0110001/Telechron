namespace Telechron.Sdk.Modules.Toolchains;

// R-DM14/R-RUN5: a Toolchain module is a descriptor, not executable code
// that runs in-process -- per R-SYS6, the actual build/test/verify
// commands it describes always execute inside an Agent container (via
// the Phase 4 IContainerExecutionService/dispatch path), never in the
// Host's ALC. This interface's job is only to answer "what command, and
// what environment does it need" for a given toolchain step; dispatching
// and running that command is the runner/dispatch layer's job.
public interface IToolchainModule : IModule
{
    // The digest-pinned Toolchain image this module's commands expect to
    // run against (R-SYS9) -- e.g. the .NET SDK image from containers/toolchains/dotnet.
    string ToolchainImageDigest { get; }

    string BuildCommand { get; }
    string TestCommand { get; }
    string VerifyCommand { get; }
    string? ExportCommand { get; }
    string? DeployCommand { get; }

    // Environment variables / working-directory expectations the
    // container must satisfy for the above commands to succeed (e.g.
    // DOTNET_NOLOGO=1) -- descriptive only, enforced by whoever builds the
    // ContainerExecutionRequest from this.
    IReadOnlyDictionary<string, string> EnvironmentRequirements { get; }
}
