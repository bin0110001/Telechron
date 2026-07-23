namespace Telechron.Host.Modules.Runtime;

public enum CanaryOutcome
{
    Healthy,
    RolledBack,
}

public sealed record CanaryWindowResult(CanaryOutcome Outcome, int TotalInvocations, int FailedInvocations, string Reason);
