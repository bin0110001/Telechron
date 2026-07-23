using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Findings;

public sealed class FailureClassifier : IFailureClassifier
{
    public FailureClassificationResult Classify(FailureClassificationInput input)
    {
        if (input.RunStatus == RunStatus.Stalled)
            return AsEnvironment("Run status is Stalled -- Agent/heartbeat infrastructure symptom, not a code failure.");

        if (input.RunStatus == RunStatus.TimedOut)
            return AsEnvironment("Run status is TimedOut -- an execution-boundary symptom, not necessarily a code defect.");

        if (input.WorkflowRunStatus == WorkflowRunStatus.TimedOut)
            return AsEnvironment("WorkflowRun status is TimedOut -- an execution-boundary symptom, not necessarily a code defect.");

        if (input.ContainerOrAgentInfrastructureFailure)
            return AsEnvironment("Container/Agent execution reported an infrastructure-level failure (network blip, resource kill, connection loss), not a workload-reported failure.");

        // A Run/WorkflowRun that completed (ran to its own conclusion,
        // even if that conclusion was Failed) and produced real output
        // text is Code -- it had the chance to fail on its own terms and
        // did.
        if (input.RunStatus is RunStatus.Failed or RunStatus.Cancelled
            || input.WorkflowRunStatus is WorkflowRunStatus.Failed or WorkflowRunStatus.PartiallyFailed)
        {
            return AsCode("Run/WorkflowRun completed and reported a failure outcome on its own terms.");
        }

        // No infrastructure signal fired and no recognized failure status
        // was supplied -- default to Code rather than silently letting an
        // unclassified failure escape repair-candidate consideration,
        // since Environment is the exemption, not the default (R-FIX8's
        // framing is "Environment findings are excluded," implying Code
        // is the baseline).
        return AsCode("No environment-failure signal present; defaulting to Code.");
    }

    private static FailureClassificationResult AsEnvironment(string reason) => new(FindingFailureClass.Environment, reason);
    private static FailureClassificationResult AsCode(string reason) => new(FindingFailureClass.Code, reason);
}
