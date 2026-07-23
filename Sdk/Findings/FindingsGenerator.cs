using System.Text.Json;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules.Runners;

namespace Telechron.Sdk.Findings;

public sealed class FindingsGenerator(IFailureClassifier classifier) : IFindingsGenerator
{
    public IReadOnlyList<Finding> GenerateFromRun(Run run, FailureClassificationInput classificationInput, CancellationToken ct = default)
    {
        var classification = classifier.Classify(classificationInput);

        // R-FIX8: an Environment-classified Run produces no repair-
        // candidate Findings at all -- not Findings-with-Environment-
        // FailureClass, literally none, since there is nothing meaningful
        // to attribute to a specific line/file when the failure was
        // infrastructure, not the workload.
        if (classification.FailureClass == FindingFailureClass.Environment)
            return [];

        if (string.IsNullOrWhiteSpace(run.SuiteResultsJson))
            return [];

        TestRunResult? testRunResult;
        try
        {
            testRunResult = JsonSerializer.Deserialize<TestRunResult>(run.SuiteResultsJson);
        }
        catch (JsonException)
        {
            return [];
        }

        if (testRunResult is null || testRunResult.Succeeded)
            return [];

        var now = DateTimeOffset.UtcNow;
        return testRunResult.TestCases
            .Where(t => t.Outcome == TestOutcome.Failed)
            .Select(t => new Finding
            {
                Id = Guid.NewGuid(),
                ProjectId = run.ProjectId,
                RunId = run.Id,
                WorkflowRunId = null,
                OriginFilePath = null, // not derivable from a bare test-case name; Verify-stage tooling narrows this later
                RootCauseSignature = BuildRootCauseSignature(t.Name, t.Message),
                Severity = FindingSeverity.Error,
                Category = "test-failure",
                FailureClass = classification.FailureClass,
                Fixability = null,
                FixStatus = null,
                CreatedAtUtc = now,
            })
            .ToList();
    }

    private static string BuildRootCauseSignature(string testName, string? message) =>
        string.IsNullOrWhiteSpace(message) ? testName : $"{testName}: {message}";

    public IReadOnlyList<Finding> GenerateFromFileLengthLint(Guid projectId, string repoRoot, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return FileLengthLintChecker.Check(repoRoot)
            .Select(v => new Finding
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                RunId = null,
                WorkflowRunId = null,
                OriginFilePath = v.RelativePath,
                RootCauseSignature = $"File exceeds the ~800 line cap ({v.LineCount} lines): {v.RelativePath}",
                Severity = FindingSeverity.Warning,
                Category = "code-quality",
                FailureClass = FindingFailureClass.Code,
                Fixability = null,
                FixStatus = null,
                CreatedAtUtc = now,
            })
            .ToList();
    }
}
