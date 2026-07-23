using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules.SelfTest;

public sealed record FalsifiabilityCheckResult(bool IsFalsifiable, string Reason);

// R-MOD4a: "A passing self-test is only meaningful if it can fail." Before
// accepting a post-patch/post-synthesis self-test pass, Verify MUST load
// the PRE-patch module version and confirm ITS self-test fails (or the
// pre-patch module doesn't even exist yet, e.g. brand-new capability --
// see IsFalsifiable=true special case below). A self-test that trivially
// passes on both old and new code (an `assert(true)`) is caught here, not
// by coverage analysis.
public interface ISelfTestFalsifiabilityChecker
{
    Task<FalsifiabilityCheckResult> CheckAsync(
        string moduleName, Guid machineId, string toolchainImageDigest,
        string preSnapshotModuleAssemblyPath, string postSnapshotModuleAssemblyPath, CancellationToken ct = default);
}
