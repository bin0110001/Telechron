using Microsoft.Extensions.Logging;
using Telechron.Host.Modules.SelfTest;
using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules;

public sealed class ModuleTrustEvaluator(
    IModuleIntegrityVerifier integrityVerifier,
    IContainerizedModuleSelfTestRunner selfTestRunner,
    ISelfTestFalsifiabilityChecker falsifiabilityChecker,
    ILogger<ModuleTrustEvaluator> logger) : IModuleTrustEvaluator
{
    public async Task<ModuleTrustResult> EvaluateAsync(
        Guid projectId, string moduleName, Guid machineId, string toolchainImageDigest, string candidateAssemblyPath,
        ModuleIntegrityManifest integrityManifest, IReadOnlyList<string> declaredCapabilities,
        IReadOnlyList<string> approvedCapabilities, string? priorInstalledAssemblyPath,
        (ModuleVersion Installed, ModuleVersion Candidate)? versionTransition = null, bool versionReapproved = false,
        CancellationToken ct = default)
    {
        // 0. R-DM7a: version compatibility -- cheapest check, no I/O, so
        // it runs before anything else touches the candidate bytes.
        if (versionTransition is { } transition)
        {
            var compatibility = ModuleVersionCompatibility.Classify(transition.Installed, transition.Candidate);
            if (compatibility == ModuleVersionCompatibilityOutcome.RequiresReapproval && !versionReapproved)
            {
                var reason = $"Major version change ({transition.Installed} -> {transition.Candidate}) requires re-approval before evaluation (R-DM7a).";
                logger.LogWarning("Module {ModuleName} rejected: {Reason}", moduleName, reason);
                return new ModuleTrustResult(ModuleTrustOutcome.MajorVersionRequiresReapproval, reason);
            }
        }

        // 1. R-MOD5a: supply-chain integrity first -- nothing else runs
        // against bytes that failed checksum/signature verification.
        var integrity = await integrityVerifier.VerifyAsync(candidateAssemblyPath, integrityManifest, ct);
        if (!integrity.IsValid)
        {
            logger.LogWarning("Module {ModuleName} rejected: integrity check failed ({Reason}).", moduleName, integrity.Reason);
            return new ModuleTrustResult(ModuleTrustOutcome.IntegrityFailed, integrity.Reason);
        }

        // 2. R-MOD8: every declared capability must already be approved.
        // Checked before any candidate code runs -- "prove you deserve an
        // unapproved capability by using it in your own self-test" would
        // be backwards.
        var approvedSet = approvedCapabilities.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var underApproved = declaredCapabilities.Where(c => !approvedSet.Contains(c)).ToList();
        if (underApproved.Count > 0)
        {
            var reason = $"Declares capabilities not yet approved for this Project: {string.Join(", ", underApproved)} (R-MOD8).";
            logger.LogWarning("Module {ModuleName} rejected: {Reason}", moduleName, reason);
            return new ModuleTrustResult(ModuleTrustOutcome.CapabilityNotApproved, reason);
        }

        // 3. R-MOD5b: maximally-restricted sandboxed self-test run --
        // grants exactly the declared (now confirmed-approved) capability
        // set, nothing more; an attempt to use anything beyond that
        // surfaces as a self-test failure, not a silent allow.
        var preTrustResult = await selfTestRunner.RunAsync(
            moduleName, machineId, toolchainImageDigest, declaredCapabilities, candidateAssemblyPath, ct);
        if (!preTrustResult.Passed)
        {
            var reason = $"Pre-trust sandboxed self-test failed: {preTrustResult.Summary}";
            logger.LogWarning("Module {ModuleName} rejected: {Reason}", moduleName, reason);
            return new ModuleTrustResult(ModuleTrustOutcome.PreTrustSelfTestFailed, reason);
        }

        // 4. R-MOD4a: falsifiability against the prior installed version,
        // if this is an update rather than a first install.
        if (priorInstalledAssemblyPath is not null)
        {
            var falsifiability = await falsifiabilityChecker.CheckAsync(
                moduleName, machineId, toolchainImageDigest, declaredCapabilities,
                priorInstalledAssemblyPath, candidateAssemblyPath, ct);
            if (!falsifiability.IsFalsifiable)
            {
                logger.LogWarning("Module {ModuleName} rejected: falsifiability check failed ({Reason}).", moduleName, falsifiability.Reason);
                return new ModuleTrustResult(ModuleTrustOutcome.FalsifiabilityCheckFailed, falsifiability.Reason);
            }
        }

        logger.LogInformation("Module {ModuleName} passed all pre-trust checks -- capabilities take effect for unrestricted execution.", moduleName);
        return new ModuleTrustResult(ModuleTrustOutcome.Trusted, "Integrity, capability approval, pre-trust sandbox, and falsifiability all passed.");
    }
}
