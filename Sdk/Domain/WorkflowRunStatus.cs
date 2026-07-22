namespace Telechron.Sdk.Domain;

// R-DM5 Workflow Run Lifecycle.
public enum WorkflowRunStatus
{
    Pending = 0,
    Running = 1,
    AwaitingApproval = 2,
    PartiallyFailed = 3,
    Passed = 4,
    Failed = 5,
    Cancelled = 6,
    TimedOut = 7,
}
