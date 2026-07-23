using System.Text.RegularExpressions;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Modules.Runners;

namespace Telechron.Modules.DotnetTestRunner;

// R-RUN1/R-RUN2: parses `dotnet test`'s console output (the xUnit VSTest
// adapter's human-readable format -- "Passed X [123 ms]" / "Failed X" /
// "Skipped X" lines plus a trailing summary) into a structured
// TestRunResult. R-RUN4: build warnings ("warning CSxxxx: ...") are
// captured as Warnings, never as failing TestCaseResults.
public sealed partial class DotnetTestRunnerModule : ITestRunnerModule
{
    public string Name => "telechron.runner.dotnet";
    public string Kind => "runner";
    public ModuleVersion Version => new(1, 0, 0);
    public IReadOnlyList<string> DeclaredCapabilities => [];
    public string SupportedToolchainKind => "dotnet";

    [GeneratedRegex(@"^\s*(Passed|Failed|Skipped)\s+(\S+)(?:\s+\[(.+)\])?\s*$", RegexOptions.Multiline)]
    private static partial Regex TestCaseLinePattern();

    [GeneratedRegex(@"^\s*warning\s+\S+:\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex WarningLinePattern();

    public TestRunResult ParseTestOutput(string stdOut, string stdErr, int? exitCode)
    {
        var combined = stdOut + Environment.NewLine + stdErr;

        var testCases = TestCaseLinePattern().Matches(combined)
            .Select(m => new TestCaseResult(
                m.Groups[2].Value,
                Enum.Parse<TestOutcome>(m.Groups[1].Value),
                m.Groups[1].Value == "Failed" ? ExtractFailureMessage(combined, m.Groups[2].Value) : null))
            .ToList();

        var warnings = WarningLinePattern().Matches(combined)
            .Select(m => m.Groups[1].Value.Trim())
            .Distinct()
            .ToList();

        // R-RUN4: succeeded is driven by actual test-case/exit-code
        // outcome, never by the presence of warnings -- a build with 40
        // warnings and all-green tests is still a success.
        var anyFailed = testCases.Any(t => t.Outcome == TestOutcome.Failed);
        var succeeded = !anyFailed && exitCode is 0 or null;

        return new TestRunResult(succeeded, testCases, warnings, combined);
    }

    private static string? ExtractFailureMessage(string combined, string testName)
    {
        // Best-effort: the line(s) immediately after "Failed <testName>"
        // typically carry "Error Message:" / assertion detail in dotnet
        // test's default console output. Not attempting full structured
        // parsing (TRX would be a more robust source) -- this is a
        // human-readable summary, not the source of truth for automated
        // pass/fail (that's TestCaseResult.Outcome itself).
        var marker = $"Failed {testName}";
        var index = combined.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0) return null;

        var afterMarker = combined[(index + marker.Length)..];
        var lines = afterMarker.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var messageLine = lines.FirstOrDefault(l => l.StartsWith("Error Message:", StringComparison.OrdinalIgnoreCase));
        return messageLine?["Error Message:".Length..].Trim();
    }

    public Task<ModuleSelfTestResult> RunSelfTestAsync(CancellationToken ct = default)
    {
        const string sample = """
            Passed  Telechron.Sdk.Tests.SolutionSkeletonTests.SdkAssembly_IsLoadable [79 ms]
            Failed  Telechron.Sdk.Tests.SomeTest.ThatFails [12 ms]
              Error Message:
               Assert.Equal() Failure
            warning CS0168: The variable 'x' is declared but never used
            Total tests: 2
                 Passed: 1
                 Failed: 1
            """;

        var result = ParseTestOutput(sample, string.Empty, exitCode: 1);

        var errors = new List<string>();
        if (result.TestCases.Count != 2) errors.Add($"Expected 2 test cases, parsed {result.TestCases.Count}.");
        if (result.TestCases.Count(t => t.Outcome == TestOutcome.Passed) != 1) errors.Add("Expected 1 passed test case.");
        if (result.TestCases.Count(t => t.Outcome == TestOutcome.Failed) != 1) errors.Add("Expected 1 failed test case.");
        if (result.Warnings.Count != 1) errors.Add($"Expected 1 warning, parsed {result.Warnings.Count}.");
        // Negative control (R-MOD4a): a run with a failed test case must
        // never be reported as succeeded, regardless of what else parses.
        if (result.Succeeded) errors.Add("Result with a failed test case must not report Succeeded=true.");

        return Task.FromResult(errors.Count == 0
            ? ModuleSelfTestResult.Success("Parsed a representative dotnet-test output sample correctly.")
            : ModuleSelfTestResult.Failure("Output parsing did not match expected structure.", [.. errors]));
    }
}
