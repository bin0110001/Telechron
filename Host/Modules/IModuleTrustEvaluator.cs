using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules;

// R-MOD5a/R-MOD5b/R-MOD8/R-MOD8a: the full pre-trust pipeline a
// newly-installed or updated module goes through before its capabilities
// take effect for unrestricted execution:
//   1. Supply-chain integrity (checksum + signature) -- refuse on mismatch.
//   2. Capability approval -- the Project must already have approved every
//      capability the module declares (R-MOD8); this is checked BEFORE the
//      sandboxed self-test runs, since running untrusted code to "prove"
//      it deserves capabilities nobody approved yet would be backwards.
//   3. Pre-trust sandboxed self-test -- maximally restricted container run.
//   4. Falsifiability check (R-MOD4a) against the prior installed version,
//      if any.
// Any stage failing short-circuits the rest; only ModuleTrustOutcome.Trusted
// means the Project's granted capabilities take effect for unrestricted
// execution (R-MOD5b) and the module becomes eligible for ModuleRuntime
// hot-reload (R-MOD7).
public interface IModuleTrustEvaluator
{
    Task<ModuleTrustResult> EvaluateAsync(
        Guid projectId,
        string moduleName,
        Guid machineId,
        string toolchainImageDigest,
        string candidateAssemblyPath,
        ModuleIntegrityManifest integrityManifest,
        IReadOnlyList<string> declaredCapabilities,
        // The capabilities the Project has actually approved for this
        // module (R-MOD8 "Projects must approve requested capabilities") --
        // for an update, this is the existing Module row's own
        // CapabilitiesJson; for a first install, it's whatever the human
        // approval flow (R-BUILD5, out of Phase 5's scope) granted. Every
        // declared capability must appear here, or evaluation stops before
        // the candidate's code ever runs.
        IReadOnlyList<string> approvedCapabilities,
        string? priorInstalledAssemblyPath,
        CancellationToken ct = default);
}
