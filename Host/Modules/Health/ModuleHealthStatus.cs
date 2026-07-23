namespace Telechron.Host.Modules.Health;

public enum ModuleHealthState
{
    // No invocations observed yet in the current rolling window.
    Unknown,
    Healthy,
    Degraded,
}

public sealed record ModuleHealthStatus(
    ModuleHealthState State, int TotalInvocations, int FailedInvocations, DateTimeOffset? LastInvocationAtUtc);
