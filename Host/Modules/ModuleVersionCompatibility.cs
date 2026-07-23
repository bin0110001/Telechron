using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules;

public enum ModuleVersionCompatibilityOutcome
{
    // Same major -- rebinds transparently, hot-reload can proceed through
    // the normal IModuleTrustEvaluator/IModuleHotReloadCoordinator path
    // without additional human sign-off beyond what those already require.
    TransparentRebind,
    // Differing major -- R-DM7a requires re-approval before this update
    // is even eligible for the trust pipeline, regardless of whether it
    // would otherwise pass integrity/capability/self-test checks.
    RequiresReapproval,
    // The candidate is not newer than what's installed -- not a valid
    // update at all (covers same-or-downgrade).
    NotAnUpgrade,
}

// R-DM7a: "same-major rebinds transparently, differing major requires
// re-approval; typed-artifact contracts stable within a major." Pure
// comparison -- no I/O, callers (module install/update flow) decide what
// "re-approval" actually means procedurally (routes to R-BUILD5's human
// gate in the eventual install workflow).
public static class ModuleVersionCompatibility
{
    public static ModuleVersionCompatibilityOutcome Classify(ModuleVersion installed, ModuleVersion candidate)
    {
        if (candidate.CompareTo(installed) <= 0)
            return ModuleVersionCompatibilityOutcome.NotAnUpgrade;

        return candidate.IsCompatibleWith(installed)
            ? ModuleVersionCompatibilityOutcome.TransparentRebind
            : ModuleVersionCompatibilityOutcome.RequiresReapproval;
    }
}
