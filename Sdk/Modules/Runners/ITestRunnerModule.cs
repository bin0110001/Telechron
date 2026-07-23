namespace Telechron.Sdk.Modules.Runners;

// R-RUN1/R-RUN2: "Test runners are pluggable and provided through
// modules." A runner's job is narrow and in-process-safe: parse the raw
// stdout/stderr a Toolchain's test command produced (already executed
// inside a container by the dispatch layer, per R-SYS6) into a
// structured TestRunResult. The runner itself never executes the test
// command -- it only interprets output already captured.
public interface ITestRunnerModule : IModule
{
    // The Toolchain Kind this runner knows how to interpret output from
    // (e.g. "dotnet") -- lets the dispatch layer pick the right runner
    // for a given Toolchain without a hardcoded switch.
    string SupportedToolchainKind { get; }

    TestRunResult ParseTestOutput(string stdOut, string stdErr, int? exitCode);
}
