using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules.SelfTest;

// R-MOD4/R-SYS6: dispatches one module self-test run into a container on
// a target Agent and returns its result. The one primitive both
// ISelfTestFalsifiabilityChecker (runs it twice, pre- and post-patch) and
// IModuleTrustEvaluator's pre-trust sandbox stage (runs it once) build on
// -- neither re-implements dispatch/blob-staging/result-parsing itself.
public interface IContainerizedModuleSelfTestRunner
{
    Task<ModuleSelfTestResult> RunAsync(
        string moduleName, Guid machineId, string toolchainImageDigest, IReadOnlyList<string> declaredCapabilities,
        string moduleAssemblyPath, CancellationToken ct = default);
}
