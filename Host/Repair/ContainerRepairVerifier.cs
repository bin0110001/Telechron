using Telechron.Sdk.Containers;
using Telechron.Sdk.Modules.Runners;
using Telechron.Sdk.Modules.Toolchains;
using Telechron.Sdk.Repair;

namespace Telechron.Host.Repair;

public sealed record ContainerRepairVerifierOptions(
    ContainerResourceLimits ResourceLimits,
    NetworkPolicy NetworkPolicy,
    TimeSpan Timeout)
{
    public static ContainerRepairVerifierOptions Default { get; } = new(
        ResourceLimits: new ContainerResourceLimits(MemoryBytes: 1024L * 1024 * 1024, CpuCores: 1.0, DiskBytes: 0),
        NetworkPolicy: new NetworkPolicy(true, []),
        Timeout: TimeSpan.FromMinutes(5));
}

// Default R-FIX2 "Verify (Build + Self-Test)" implementation. Runs the
// Project's own IToolchainModule.TestCommand inside the real Phase 4
// container boundary (R-SYS6), then hands the raw output to the matching
// ITestRunnerModule to parse -- the exact two-step shape Phase 6's
// ToolchainContainerRunLiveTests proved, reused here rather than
// reimplemented (R-ENG4: no duplicated repair/verification primitives).
public sealed class ContainerRepairVerifier(
    IContainerExecutionService containerExecutionService,
    IToolchainModule toolchain,
    ITestRunnerModule testRunner,
    ContainerRepairVerifierOptions options) : IRepairVerifier
{
    public ContainerRepairVerifier(
        IContainerExecutionService containerExecutionService, IToolchainModule toolchain, ITestRunnerModule testRunner)
        : this(containerExecutionService, toolchain, testRunner, ContainerRepairVerifierOptions.Default)
    {
    }

    public async Task<VerifyResult> VerifyAsync(string projectRootPath, CancellationToken ct = default)
    {
        var containerResult = await containerExecutionService.ExecuteAsync(new ContainerExecutionRequest(
            ImageDigest: toolchain.ToolchainImageDigest,
            Command: ["/bin/sh", "-c", $"cd /workspace && {toolchain.TestCommand}"],
            WorkingDirectoryHostPath: projectRootPath,
            ResourceLimits: options.ResourceLimits,
            NetworkPolicy: options.NetworkPolicy,
            RequiresGpu: false,
            Timeout: options.Timeout), ct);

        if (containerResult.Outcome != ContainerExecutionOutcome.Completed)
        {
            return new VerifyResult(false, null,
                $"Container did not complete (Outcome={containerResult.Outcome}). StdOut: {containerResult.StdOut}\nStdErr: {containerResult.StdErr}");
        }

        var testRunResult = testRunner.ParseTestOutput(containerResult.StdOut, containerResult.StdErr, containerResult.ExitCode);
        return new VerifyResult(testRunResult.Succeeded, testRunResult, containerResult.StdOut);
    }
}
