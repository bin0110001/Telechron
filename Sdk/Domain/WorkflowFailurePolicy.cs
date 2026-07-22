namespace Telechron.Sdk.Domain;

// R-WF3: how a graph Workflow Run's aggregate status is derived from branch
// failures. FailFast escalates any branch failure to Failed; ContinueOnError
// yields PartiallyFailed when at least one branch fails and at least one
// succeeds (R-DM5).
public enum WorkflowFailurePolicy
{
    FailFast = 0,
    ContinueOnError = 1,
}
