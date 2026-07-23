using Telechron.Sdk.Domain;
using Telechron.Sdk.Findings;

namespace Telechron.Sdk.Tests.Findings;

public class FailureClassifierTests
{
    private readonly FailureClassifier _classifier = new();

    [Fact]
    public void Classify_StalledRun_IsEnvironment()
    {
        var result = _classifier.Classify(new FailureClassificationInput(RunStatus.Stalled, null, false, null));

        Assert.Equal(FindingFailureClass.Environment, result.FailureClass);
    }

    [Fact]
    public void Classify_TimedOutRun_IsEnvironment()
    {
        var result = _classifier.Classify(new FailureClassificationInput(RunStatus.TimedOut, null, false, null));

        Assert.Equal(FindingFailureClass.Environment, result.FailureClass);
    }

    [Fact]
    public void Classify_TimedOutWorkflowRun_IsEnvironment()
    {
        var result = _classifier.Classify(new FailureClassificationInput(null, WorkflowRunStatus.TimedOut, false, null));

        Assert.Equal(FindingFailureClass.Environment, result.FailureClass);
    }

    [Fact]
    public void Classify_ContainerInfrastructureFailure_IsEnvironment()
    {
        var result = _classifier.Classify(new FailureClassificationInput(RunStatus.Failed, null, true, "some output"));

        // Infrastructure failure signal takes precedence even though the
        // Run itself is nominally "Failed" -- R-FIX8's actual concern is a
        // network blip/heartbeat-loss symptom masquerading as a code failure.
        Assert.Equal(FindingFailureClass.Environment, result.FailureClass);
    }

    [Fact]
    public void Classify_FailedRunWithNoInfrastructureSignal_IsCode()
    {
        var result = _classifier.Classify(new FailureClassificationInput(RunStatus.Failed, null, false, "assertion failed"));

        Assert.Equal(FindingFailureClass.Code, result.FailureClass);
    }

    [Fact]
    public void Classify_PartiallyFailedWorkflowRun_IsCode()
    {
        var result = _classifier.Classify(new FailureClassificationInput(null, WorkflowRunStatus.PartiallyFailed, false, null));

        Assert.Equal(FindingFailureClass.Code, result.FailureClass);
    }

    [Fact]
    public void Classify_CancelledRun_IsCode()
    {
        // Cancelled isn't infrastructure flakiness -- it's an explicit
        // stop, which the design treats as a completed-on-its-own-terms
        // outcome rather than an environment symptom.
        var result = _classifier.Classify(new FailureClassificationInput(RunStatus.Cancelled, null, false, null));

        Assert.Equal(FindingFailureClass.Code, result.FailureClass);
    }

    [Fact]
    public void Classify_PassedRunWithNoFailureSignal_DefaultsToCode()
    {
        var result = _classifier.Classify(new FailureClassificationInput(RunStatus.Passed, null, false, null));

        Assert.Equal(FindingFailureClass.Code, result.FailureClass);
    }
}
