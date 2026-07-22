namespace Telechron.Sdk.Domain;

// R-DM2 Run lifecycle.
public enum RunStatus
{
    Pending = 0,
    Running = 1,
    Passed = 2,
    Failed = 3,
    Cancelled = 4,
    TimedOut = 5,
    Stalled = 6,
}
