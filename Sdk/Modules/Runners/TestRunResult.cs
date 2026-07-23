namespace Telechron.Sdk.Modules.Runners;

public enum TestOutcome
{
    Passed,
    Failed,
    Skipped,
}

public sealed record TestCaseResult(string Name, TestOutcome Outcome, string? Message);

// R-RUN4: "Warnings and informational findings do not constitute
// failures." Warnings is a distinct bucket from Failed test cases --
// a runner that only ever produces Failed/Passed would have nowhere
// non-failing to put a warning, and would be tempted to either drop it
// or misclassify it as a failure.
public sealed record TestRunResult(
    bool Succeeded, IReadOnlyList<TestCaseResult> TestCases, IReadOnlyList<string> Warnings, string RawOutput);
