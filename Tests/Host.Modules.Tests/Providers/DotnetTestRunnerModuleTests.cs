using Telechron.Modules.DotnetTestRunner;
using Telechron.Sdk.Modules.Runners;

namespace Telechron.Host.Modules.Tests.Providers;

public class DotnetTestRunnerModuleTests
{
    private readonly DotnetTestRunnerModule _runner = new();

    [Fact]
    public async Task RunSelfTestAsync_Passes()
    {
        var result = await _runner.RunSelfTestAsync();

        Assert.True(result.Passed, string.Join("; ", result.Errors));
    }

    [Fact]
    public void ParseTestOutput_RealDotnetTestOutput_AllPassing_ParsesCorrectly()
    {
        // Captured verbatim from an actual `dotnet test` run in this
        // session (Tests/Sdk.Tests) -- not a hand-crafted sample.
        const string realOutput = """
              Determining projects to restore...
              All projects are up-to-date for restore.
              Telechron.Sdk -> C:\Projects\Telechron\Sdk\bin\Debug\net10.0\Telechron.Sdk.dll
              Telechron.Sdk.Tests -> C:\Projects\Telechron\Tests\Sdk.Tests\bin\Debug\net10.0\Telechron.Sdk.Tests.dll
            Test run for C:\Projects\Telechron\Tests\Sdk.Tests\bin\Debug\net10.0\Telechron.Sdk.Tests.dll (.NETCoreApp,Version=v10.0)
            A total of 1 test files matched the specified pattern.
            [xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v3.1.4+50e68bbb8b (64-bit .NET 10.0.10)
            [xUnit.net 00:00:00.06]   Discovering: Telechron.Sdk.Tests
            [xUnit.net 00:00:00.09]   Discovered:  Telechron.Sdk.Tests
            [xUnit.net 00:00:00.10]   Starting:    Telechron.Sdk.Tests
            [xUnit.net 00:00:00.21]   Finished:    Telechron.Sdk.Tests
              Passed Telechron.Sdk.Tests.SolutionSkeletonTests.SdkAssembly_IsLoadable [79 ms]

            Test Run Successful.
            Total tests: 1
                 Passed: 1
             Total time: 0.6588 Seconds
            """;

        var result = _runner.ParseTestOutput(realOutput, string.Empty, exitCode: 0);

        Assert.True(result.Succeeded);
        Assert.Single(result.TestCases);
        Assert.Equal("Telechron.Sdk.Tests.SolutionSkeletonTests.SdkAssembly_IsLoadable", result.TestCases[0].Name);
        Assert.Equal(TestOutcome.Passed, result.TestCases[0].Outcome);
    }

    [Fact]
    public void ParseTestOutput_FailingTest_ReportsFailedAndNotSucceeded()
    {
        const string output = """
              Failed Telechron.Sdk.Tests.SomeTest.ThatFails [12 ms]
              Error Message:
               Assert.Equal() Failure
              Expected: 4
              Actual:   0

            Test Run Failed.
            Total tests: 1
                 Failed: 1
            """;

        var result = _runner.ParseTestOutput(output, string.Empty, exitCode: 1);

        Assert.False(result.Succeeded);
        Assert.Single(result.TestCases);
        Assert.Equal(TestOutcome.Failed, result.TestCases[0].Outcome);
    }

    [Fact]
    public void ParseTestOutput_WarningsPresent_DoesNotCountAsFailure()
    {
        const string output = """
            warning CS0168: The variable 'x' is declared but never used
              Passed Telechron.Sdk.Tests.SolutionSkeletonTests.SdkAssembly_IsLoadable [79 ms]

            Test Run Successful.
            Total tests: 1
                 Passed: 1
            """;

        var result = _runner.ParseTestOutput(output, string.Empty, exitCode: 0);

        Assert.True(result.Succeeded);
        Assert.Single(result.Warnings);
        Assert.Contains("declared but never used", result.Warnings[0]);
        // R-RUN4: the warning must not show up as a failing test case.
        Assert.DoesNotContain(result.TestCases, t => t.Outcome == TestOutcome.Failed);
    }

    [Fact]
    public void ParseTestOutput_SkippedTest_IsDistinctFromFailed()
    {
        const string output = """
              Skipped Telechron.Sdk.Tests.SomeTest.NotYetImplemented

            Total tests: 1
                 Skipped: 1
            """;

        var result = _runner.ParseTestOutput(output, string.Empty, exitCode: 0);

        Assert.Single(result.TestCases);
        Assert.Equal(TestOutcome.Skipped, result.TestCases[0].Outcome);
        Assert.True(result.Succeeded);
    }
}
