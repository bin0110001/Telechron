namespace Telechron.Sdk.Containers;

public enum ContainerExecutionOutcome
{
    Completed,
    TimedOut,
    ResourceLimitExceeded,
    Failed,
}

public sealed record ContainerExecutionResult(
    ContainerExecutionOutcome Outcome,
    int? ExitCode,
    string StdOut,
    string StdErr,
    TimeSpan Duration);
