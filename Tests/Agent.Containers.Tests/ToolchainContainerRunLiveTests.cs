using Docker.DotNet;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telechron.Modules.DotnetTestRunner;
using Telechron.Modules.DotnetToolchain;
using Telechron.Sdk.Containers;
using Telechron.Sdk.Modules.Runners;

namespace Telechron.Agent.Containers.Tests;

// Phase 6 exit criteria, live: "a container-run test suite against a real
// Toolchain produces results." Uses the REAL DotnetToolchainModule
// descriptor (its own ToolchainImageDigest/TestCommand, not a hand-picked
// image/command), the REAL Phase 4 container execution boundary, and the
// REAL DotnetTestRunnerModule's output parser -- a genuine end-to-end
// chain through every piece Phase 6 built for R-RUN1/2/5, not separately
// mocked stages. Skips if Podman isn't reachable.
public class ToolchainContainerRunLiveTests : IAsyncLifetime
{
    private IDockerClient _dockerClient = null!;
    private bool _podmanAvailable;
    private string _workspaceDir = null!;

    public async Task InitializeAsync()
    {
        _dockerClient = new DockerClientConfiguration(new Uri("npipe://./pipe/podman-machine-default")).CreateClient();
        _workspaceDir = Path.Combine(Path.GetTempPath(), "telechron-toolchain-livetest-" + Guid.NewGuid());
        Directory.CreateDirectory(_workspaceDir);

        try
        {
            await _dockerClient.System.PingAsync();
            _podmanAvailable = true;
        }
        catch
        {
            _podmanAvailable = false;
        }
    }

    public Task DisposeAsync()
    {
        _dockerClient.Dispose();
        if (Directory.Exists(_workspaceDir))
            Directory.Delete(_workspaceDir, recursive: true);
        return Task.CompletedTask;
    }

    private sealed class NoOpGpuStateSanitizer : IGpuStateSanitizer
    {
        public Task SanitizeAsync(IReadOnlyList<string> gpuDeviceIds, CancellationToken ct = default) => Task.CompletedTask;
    }

    private PodmanContainerExecutionService CreateExecutionService() =>
        new(_dockerClient,
            new ImageProvenanceVerifier(Options.Create(new RegistryAllowlist())),
            Options.Create(new GpuTenancyPolicy()),
            new UnimplementedGpuCapabilityGate(),
            new NoOpGpuStateSanitizer(),
            new PassthroughWarmContainerPool(),
            NullLogger<PodmanContainerExecutionService>.Instance);

    private async Task WriteTestProjectAsync(bool includeFailingTest)
    {
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
                <Nullable>enable</Nullable>
                <IsPackable>false</IsPackable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
                <PackageReference Include="xunit" Version="2.9.0" />
                <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
              </ItemGroup>
            </Project>
            """;
        await File.WriteAllTextAsync(Path.Combine(_workspaceDir, "SampleTests.csproj"), csproj);

        var failingTestMethod = includeFailingTest
            ? """

                [Fact]
                public void ThisTestIntentionallyFails()
                {
                    Assert.Equal(4, 2 + 1);
                }
                """
            : string.Empty;

        var testSource = $$"""
            using Xunit;

            public class SampleTests
            {
                [Fact]
                public void Addition_Works()
                {
                    Assert.Equal(4, 2 + 2);
                }
                {{failingTestMethod}}
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_workspaceDir, "SampleTests.cs"), testSource);
    }

    [SkippableFact]
    public async Task DotnetToolchainTestCommand_RunInRealContainer_ProducesRealParsedResults()
    {
        Skip.IfNot(_podmanAvailable, "Podman machine not reachable at npipe://./pipe/podman-machine-default");

        await WriteTestProjectAsync(includeFailingTest: false);

        var toolchain = new DotnetToolchainModule();
        var executionService = CreateExecutionService();

        var result = await executionService.ExecuteAsync(new ContainerExecutionRequest(
            ImageDigest: toolchain.ToolchainImageDigest,
            Command: ["/bin/sh", "-c", $"cd /workspace && {toolchain.TestCommand} --logger \"console;verbosity=normal\""],
            WorkingDirectoryHostPath: _workspaceDir,
            ResourceLimits: new ContainerResourceLimits(MemoryBytes: 1024 * 1024 * 1024, CpuCores: 1.0, DiskBytes: 0),
            // dotnet test needs to restore NuGet packages -- this Toolchain
            // run legitimately needs network, unlike the untrusted-code
            // default-deny case R-SYS7 governs.
            NetworkPolicy: new NetworkPolicy(true, []),
            RequiresGpu: false,
            Timeout: TimeSpan.FromMinutes(5)));

        Assert.Equal(ContainerExecutionOutcome.Completed, result.Outcome);

        var runner = new DotnetTestRunnerModule();
        var parsed = runner.ParseTestOutput(result.StdOut, result.StdErr, result.ExitCode);

        Assert.True(parsed.Succeeded, $"Expected success. StdOut: {result.StdOut}\nStdErr: {result.StdErr}");
        Assert.Contains(parsed.TestCases, t => t.Name.Contains("Addition_Works") && t.Outcome == TestOutcome.Passed);
    }

    [SkippableFact]
    public async Task DotnetToolchainTestCommand_WithFailingTest_ProducesRealFailedResult()
    {
        Skip.IfNot(_podmanAvailable, "Podman machine not reachable at npipe://./pipe/podman-machine-default");

        await WriteTestProjectAsync(includeFailingTest: true);

        var toolchain = new DotnetToolchainModule();
        var executionService = CreateExecutionService();

        var result = await executionService.ExecuteAsync(new ContainerExecutionRequest(
            ImageDigest: toolchain.ToolchainImageDigest,
            Command: ["/bin/sh", "-c", $"cd /workspace && {toolchain.TestCommand} --logger \"console;verbosity=normal\""],
            WorkingDirectoryHostPath: _workspaceDir,
            ResourceLimits: new ContainerResourceLimits(MemoryBytes: 1024 * 1024 * 1024, CpuCores: 1.0, DiskBytes: 0),
            NetworkPolicy: new NetworkPolicy(true, []),
            RequiresGpu: false,
            Timeout: TimeSpan.FromMinutes(5)));

        // dotnet test exits non-zero on a failing test -- Completed is
        // still the right Outcome (the container ran and finished; the
        // TEST SUITE failing is a different concept from the CONTAINER
        // failing, which is exactly why TestRunResult.Succeeded is its
        // own field rather than reusing ContainerExecutionOutcome).
        Assert.Equal(ContainerExecutionOutcome.Completed, result.Outcome);

        var runner = new DotnetTestRunnerModule();
        var parsed = runner.ParseTestOutput(result.StdOut, result.StdErr, result.ExitCode);

        Assert.False(parsed.Succeeded);
        Assert.Contains(parsed.TestCases, t => t.Name.Contains("ThisTestIntentionallyFails") && t.Outcome == TestOutcome.Failed);
        Assert.Contains(parsed.TestCases, t => t.Name.Contains("Addition_Works") && t.Outcome == TestOutcome.Passed);
    }
}
