using System.Text.Json;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Findings;
using Telechron.Sdk.Modules.Runners;

namespace Telechron.Sdk.Tests.Findings;

public class FindingsGeneratorTests
{
    private readonly FindingsGenerator _generator = new(new FailureClassifier());

    private static Run MakeRun(RunStatus status, string? suiteResultsJson) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        Status = status,
        SuiteResultsJson = suiteResultsJson,
    };

    private static string SerializeSuiteResults(TestRunResult result) => JsonSerializer.Serialize(result);

    [Fact]
    public void GenerateFromRun_EnvironmentClassifiedRun_ProducesNoFindings()
    {
        var testRunResult = new TestRunResult(
            Succeeded: false,
            TestCases: [new TestCaseResult("Namespace.Test1", TestOutcome.Failed, "boom")],
            Warnings: [],
            RawOutput: "raw");
        var run = MakeRun(RunStatus.Stalled, SerializeSuiteResults(testRunResult));

        var findings = _generator.GenerateFromRun(run, new FailureClassificationInput(RunStatus.Stalled, null, false, null));

        Assert.Empty(findings);
    }

    [Fact]
    public void GenerateFromRun_OneFailedTestCase_ProducesOneCodeFinding()
    {
        var testRunResult = new TestRunResult(
            Succeeded: false,
            TestCases:
            [
                new TestCaseResult("Namespace.PassingTest", TestOutcome.Passed, null),
                new TestCaseResult("Namespace.FailingTest", TestOutcome.Failed, "Expected 4 but got 5"),
            ],
            Warnings: [],
            RawOutput: "raw output text");
        var run = MakeRun(RunStatus.Failed, SerializeSuiteResults(testRunResult));

        var findings = _generator.GenerateFromRun(run, new FailureClassificationInput(RunStatus.Failed, null, false, "raw output text"));

        var finding = Assert.Single(findings);
        Assert.Equal(run.Id, finding.RunId);
        Assert.Equal(run.ProjectId, finding.ProjectId);
        Assert.Equal(FindingFailureClass.Code, finding.FailureClass);
        Assert.Equal("test-failure", finding.Category);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Equal("Namespace.FailingTest: Expected 4 but got 5", finding.RootCauseSignature);
    }

    [Fact]
    public void GenerateFromRun_MultipleFailedTestCases_ProducesOneFindingPerFailure()
    {
        var testRunResult = new TestRunResult(
            Succeeded: false,
            TestCases:
            [
                new TestCaseResult("Namespace.FailingTest1", TestOutcome.Failed, "first failure"),
                new TestCaseResult("Namespace.FailingTest2", TestOutcome.Failed, "second failure"),
                new TestCaseResult("Namespace.SkippedTest", TestOutcome.Skipped, null),
                new TestCaseResult("Namespace.PassingTest", TestOutcome.Passed, null),
            ],
            Warnings: [],
            RawOutput: "raw");
        var run = MakeRun(RunStatus.Failed, SerializeSuiteResults(testRunResult));

        var findings = _generator.GenerateFromRun(run, new FailureClassificationInput(RunStatus.Failed, null, false, "raw"));

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.RootCauseSignature == "Namespace.FailingTest1: first failure");
        Assert.Contains(findings, f => f.RootCauseSignature == "Namespace.FailingTest2: second failure");
    }

    [Fact]
    public void GenerateFromRun_SucceededTestRunResult_ProducesNoFindings()
    {
        var testRunResult = new TestRunResult(Succeeded: true, TestCases: [], Warnings: [], RawOutput: "all good");
        var run = MakeRun(RunStatus.Passed, SerializeSuiteResults(testRunResult));

        var findings = _generator.GenerateFromRun(run, new FailureClassificationInput(RunStatus.Passed, null, false, "all good"));

        Assert.Empty(findings);
    }

    [Fact]
    public void GenerateFromRun_NullSuiteResultsJson_ProducesNoFindings()
    {
        var run = MakeRun(RunStatus.Failed, null);

        var findings = _generator.GenerateFromRun(run, new FailureClassificationInput(RunStatus.Failed, null, false, null));

        Assert.Empty(findings);
    }

    [Fact]
    public void GenerateFromRun_MalformedSuiteResultsJson_ProducesNoFindingsRatherThanThrowing()
    {
        var run = MakeRun(RunStatus.Failed, "{ not valid json ][");

        var findings = _generator.GenerateFromRun(run, new FailureClassificationInput(RunStatus.Failed, null, false, "n/a"));

        Assert.Empty(findings);
    }

    [Fact]
    public void GenerateFromFileLengthLint_ViolationsFound_ProducesWarningCodeFindings()
    {
        var tempRoot = Directory.CreateTempSubdirectory("telechron-findings-lint-");
        try
        {
            var longFile = Path.Combine(tempRoot.FullName, "TooLong.cs");
            File.WriteAllLines(longFile, Enumerable.Repeat("// line", 801));

            var projectId = Guid.NewGuid();
            var findings = _generator.GenerateFromFileLengthLint(projectId, tempRoot.FullName);

            var finding = Assert.Single(findings);
            Assert.Equal(projectId, finding.ProjectId);
            Assert.Null(finding.RunId);
            Assert.Equal("TooLong.cs", finding.OriginFilePath);
            Assert.Equal(FindingFailureClass.Code, finding.FailureClass);
            Assert.Equal(FindingSeverity.Warning, finding.Severity);
            Assert.Equal("code-quality", finding.Category);
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }
}
