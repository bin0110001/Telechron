namespace Telechron.Host.Modules.Runtime;

public enum ModuleDrainOutcome
{
    DrainedCleanly,
    DrainTimedOutAndCancelled,
    LeakDetected,
}

public sealed record ModuleDrainResult(ModuleDrainOutcome Outcome, int InFlightAtTimeout);
