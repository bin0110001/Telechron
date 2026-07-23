using Microsoft.Extensions.Logging;

namespace Telechron.Host.Modules.SelfTest;

// R-MOD4a: runs the module's self-test (via IContainerizedModuleSelfTestRunner,
// R-SYS6 -- never in-process on the Host; ModuleRuntime's ALC is
// lifecycle-only per R-MOD7) for both the pre-patch and post-patch
// assembly, and confirms the pre-patch run fails before trusting the
// post-patch pass.
public sealed class SelfTestFalsifiabilityChecker(
    IContainerizedModuleSelfTestRunner selfTestRunner, ILogger<SelfTestFalsifiabilityChecker> logger)
    : ISelfTestFalsifiabilityChecker
{
    public async Task<FalsifiabilityCheckResult> CheckAsync(
        string moduleName, Guid machineId, string toolchainImageDigest, IReadOnlyList<string> declaredCapabilities,
        string preSnapshotModuleAssemblyPath, string postSnapshotModuleAssemblyPath, CancellationToken ct = default)
    {
        if (!File.Exists(preSnapshotModuleAssemblyPath))
        {
            // Brand-new capability -- there is no "pre" to falsify against.
            // Falsifiability is vacuously satisfied; the negative control
            // burden shifts entirely to whether the post self-test is
            // itself non-trivial (a later, capability-specific concern).
            return new FalsifiabilityCheckResult(IsFalsifiable: true,
                "No pre-patch snapshot exists (new capability) -- falsifiability check is not applicable.");
        }

        var preResult = await selfTestRunner.RunAsync(
            moduleName, machineId, toolchainImageDigest, declaredCapabilities, preSnapshotModuleAssemblyPath, ct);
        if (preResult.Passed)
        {
            logger.LogWarning(
                "Self-test falsifiability check FAILED: pre-patch snapshot's self-test also passes ({Summary}) -- not a negative control (R-MOD4a).",
                preResult.Summary);
            return new FalsifiabilityCheckResult(IsFalsifiable: false,
                "Self-test passes against the pre-patch snapshot too -- it cannot distinguish broken code from fixed code (R-MOD4a).");
        }

        var postResult = await selfTestRunner.RunAsync(
            moduleName, machineId, toolchainImageDigest, declaredCapabilities, postSnapshotModuleAssemblyPath, ct);
        if (!postResult.Passed)
        {
            return new FalsifiabilityCheckResult(IsFalsifiable: false,
                $"Post-patch self-test still fails: {postResult.Summary}");
        }

        return new FalsifiabilityCheckResult(IsFalsifiable: true,
            "Self-test fails on the pre-patch snapshot and passes on the post-patch snapshot -- valid negative control.");
    }
}
