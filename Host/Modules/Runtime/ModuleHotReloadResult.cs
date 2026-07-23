namespace Telechron.Host.Modules.Runtime;

public enum ModuleHotReloadOutcome
{
    ReloadedSuccessfully,
    RolledBackAfterCanaryFailure,
    RolledBackAfterLoadFailure,
}

public sealed record ModuleHotReloadResult(
    ModuleHotReloadOutcome Outcome,
    ModuleDrainResult DrainResult,
    bool OldVersionUnloadLeakDetected,
    CanaryWindowResult? CanaryResult,
    string Reason);
