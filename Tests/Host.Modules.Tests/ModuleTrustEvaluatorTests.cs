using Microsoft.Extensions.Logging.Abstractions;
using Telechron.Host.Modules;
using Telechron.Host.Modules.SelfTest;
using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules.Tests;

public class ModuleTrustEvaluatorTests
{
    private static readonly ModuleIntegrityManifest ValidManifest = new("key", "checksum", "sig");

    private sealed class StubIntegrityVerifier(bool isValid) : IModuleIntegrityVerifier
    {
        public Task<IntegrityVerificationResult> VerifyAsync(string path, ModuleIntegrityManifest manifest, CancellationToken ct = default) =>
            Task.FromResult(new IntegrityVerificationResult(isValid, isValid ? "ok" : "integrity failed"));
    }

    private sealed class StubSelfTestRunner(bool passes) : IContainerizedModuleSelfTestRunner
    {
        public List<IReadOnlyList<string>> CapabilitiesPassedPerCall { get; } = [];

        public Task<ModuleSelfTestResult> RunAsync(
            string moduleName, Guid machineId, string toolchainImageDigest, IReadOnlyList<string> declaredCapabilities,
            string moduleAssemblyPath, CancellationToken ct = default)
        {
            CapabilitiesPassedPerCall.Add(declaredCapabilities);
            return Task.FromResult(passes
                ? ModuleSelfTestResult.Success("ok")
                : ModuleSelfTestResult.Failure("self-test failed"));
        }
    }

    private sealed class StubFalsifiabilityChecker(bool isFalsifiable) : ISelfTestFalsifiabilityChecker
    {
        public Task<FalsifiabilityCheckResult> CheckAsync(
            string moduleName, Guid machineId, string toolchainImageDigest, IReadOnlyList<string> declaredCapabilities,
            string preSnapshotModuleAssemblyPath, string postSnapshotModuleAssemblyPath, CancellationToken ct = default) =>
            Task.FromResult(new FalsifiabilityCheckResult(isFalsifiable, isFalsifiable ? "ok" : "not falsifiable"));
    }

    private static string DummyAssemblyPath()
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    [Fact]
    public async Task EvaluateAsync_IntegrityCheckFails_ShortCircuitsAsIntegrityFailed()
    {
        var evaluator = new ModuleTrustEvaluator(
            new StubIntegrityVerifier(isValid: false),
            new StubSelfTestRunner(passes: true),
            new StubFalsifiabilityChecker(isFalsifiable: true),
            NullLogger<ModuleTrustEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(
            Guid.NewGuid(), "mod", Guid.NewGuid(), "img@sha256:" + new string('a', 64), DummyAssemblyPath(),
            ValidManifest, declaredCapabilities: [], approvedCapabilities: [], priorInstalledAssemblyPath: null);

        Assert.Equal(ModuleTrustOutcome.IntegrityFailed, result.Outcome);
        Assert.False(result.IsTrusted);
    }

    [Fact]
    public async Task EvaluateAsync_DeclaredCapabilityNotApproved_IsRejectedWithoutRunningSelfTest()
    {
        var selfTestRunner = new StubSelfTestRunner(passes: true);
        var evaluator = new ModuleTrustEvaluator(
            new StubIntegrityVerifier(isValid: true),
            selfTestRunner,
            new StubFalsifiabilityChecker(isFalsifiable: true),
            NullLogger<ModuleTrustEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(
            Guid.NewGuid(), "mod", Guid.NewGuid(), "img@sha256:" + new string('a', 64), DummyAssemblyPath(),
            ValidManifest, declaredCapabilities: ["InternetAccess"], approvedCapabilities: ["FilesystemRead"],
            priorInstalledAssemblyPath: null);

        Assert.Equal(ModuleTrustOutcome.CapabilityNotApproved, result.Outcome);
        Assert.Contains("InternetAccess", result.Reason);
        // Running untrusted code to "prove" it deserves an unapproved
        // capability would be backwards -- the self-test must never run.
        Assert.Empty(selfTestRunner.CapabilitiesPassedPerCall);
    }

    [Fact]
    public async Task EvaluateAsync_DeclaredCapabilitySubsetOfApproved_PassesApprovalGate()
    {
        var selfTestRunner = new StubSelfTestRunner(passes: true);
        var evaluator = new ModuleTrustEvaluator(
            new StubIntegrityVerifier(isValid: true),
            selfTestRunner,
            new StubFalsifiabilityChecker(isFalsifiable: true),
            NullLogger<ModuleTrustEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(
            Guid.NewGuid(), "mod", Guid.NewGuid(), "img@sha256:" + new string('a', 64), DummyAssemblyPath(),
            ValidManifest, declaredCapabilities: ["FilesystemRead"], approvedCapabilities: ["FilesystemRead", "InternetAccess"],
            priorInstalledAssemblyPath: null);

        Assert.Equal(ModuleTrustOutcome.Trusted, result.Outcome);
        Assert.Single(selfTestRunner.CapabilitiesPassedPerCall);
    }

    [Fact]
    public async Task EvaluateAsync_PreTrustSelfTestFails_IsRejected()
    {
        var evaluator = new ModuleTrustEvaluator(
            new StubIntegrityVerifier(isValid: true),
            new StubSelfTestRunner(passes: false),
            new StubFalsifiabilityChecker(isFalsifiable: true),
            NullLogger<ModuleTrustEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(
            Guid.NewGuid(), "mod", Guid.NewGuid(), "img@sha256:" + new string('a', 64), DummyAssemblyPath(),
            ValidManifest, declaredCapabilities: [], approvedCapabilities: [], priorInstalledAssemblyPath: null);

        Assert.Equal(ModuleTrustOutcome.PreTrustSelfTestFailed, result.Outcome);
    }

    [Fact]
    public async Task EvaluateAsync_NoPriorVersion_SkipsFalsifiabilityCheck()
    {
        var evaluator = new ModuleTrustEvaluator(
            new StubIntegrityVerifier(isValid: true),
            new StubSelfTestRunner(passes: true),
            new StubFalsifiabilityChecker(isFalsifiable: false), // would reject if it ran
            NullLogger<ModuleTrustEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(
            Guid.NewGuid(), "mod", Guid.NewGuid(), "img@sha256:" + new string('a', 64), DummyAssemblyPath(),
            ValidManifest, declaredCapabilities: [], approvedCapabilities: [], priorInstalledAssemblyPath: null);

        Assert.Equal(ModuleTrustOutcome.Trusted, result.Outcome);
    }

    [Fact]
    public async Task EvaluateAsync_PriorVersionExistsAndFalsifiabilityFails_IsRejected()
    {
        var evaluator = new ModuleTrustEvaluator(
            new StubIntegrityVerifier(isValid: true),
            new StubSelfTestRunner(passes: true),
            new StubFalsifiabilityChecker(isFalsifiable: false),
            NullLogger<ModuleTrustEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(
            Guid.NewGuid(), "mod", Guid.NewGuid(), "img@sha256:" + new string('a', 64), DummyAssemblyPath(),
            ValidManifest, declaredCapabilities: [], approvedCapabilities: [], priorInstalledAssemblyPath: DummyAssemblyPath());

        Assert.Equal(ModuleTrustOutcome.FalsifiabilityCheckFailed, result.Outcome);
    }

    [Fact]
    public async Task EvaluateAsync_AllChecksPass_IsTrusted()
    {
        var evaluator = new ModuleTrustEvaluator(
            new StubIntegrityVerifier(isValid: true),
            new StubSelfTestRunner(passes: true),
            new StubFalsifiabilityChecker(isFalsifiable: true),
            NullLogger<ModuleTrustEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(
            Guid.NewGuid(), "mod", Guid.NewGuid(), "img@sha256:" + new string('a', 64), DummyAssemblyPath(),
            ValidManifest, declaredCapabilities: ["FilesystemRead"], approvedCapabilities: ["FilesystemRead"],
            priorInstalledAssemblyPath: DummyAssemblyPath());

        Assert.True(result.IsTrusted);
    }

    [Fact]
    public async Task EvaluateAsync_MajorVersionBumpWithoutReapproval_IsRejectedBeforeIntegrityCheck()
    {
        var integrityVerifier = new StubIntegrityVerifier(isValid: true);
        var evaluator = new ModuleTrustEvaluator(
            integrityVerifier,
            new StubSelfTestRunner(passes: true),
            new StubFalsifiabilityChecker(isFalsifiable: true),
            NullLogger<ModuleTrustEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(
            Guid.NewGuid(), "mod", Guid.NewGuid(), "img@sha256:" + new string('a', 64), DummyAssemblyPath(),
            ValidManifest, declaredCapabilities: [], approvedCapabilities: [], priorInstalledAssemblyPath: null,
            versionTransition: (new ModuleVersion(1, 0, 0), new ModuleVersion(2, 0, 0)), versionReapproved: false);

        Assert.Equal(ModuleTrustOutcome.MajorVersionRequiresReapproval, result.Outcome);
    }

    [Fact]
    public async Task EvaluateAsync_MajorVersionBumpWithReapproval_ProceedsToOtherChecks()
    {
        var evaluator = new ModuleTrustEvaluator(
            new StubIntegrityVerifier(isValid: true),
            new StubSelfTestRunner(passes: true),
            new StubFalsifiabilityChecker(isFalsifiable: true),
            NullLogger<ModuleTrustEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(
            Guid.NewGuid(), "mod", Guid.NewGuid(), "img@sha256:" + new string('a', 64), DummyAssemblyPath(),
            ValidManifest, declaredCapabilities: [], approvedCapabilities: [], priorInstalledAssemblyPath: null,
            versionTransition: (new ModuleVersion(1, 0, 0), new ModuleVersion(2, 0, 0)), versionReapproved: true);

        Assert.True(result.IsTrusted);
    }

    [Fact]
    public async Task EvaluateAsync_SameMajorVersionBump_ProceedsWithoutReapproval()
    {
        var evaluator = new ModuleTrustEvaluator(
            new StubIntegrityVerifier(isValid: true),
            new StubSelfTestRunner(passes: true),
            new StubFalsifiabilityChecker(isFalsifiable: true),
            NullLogger<ModuleTrustEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(
            Guid.NewGuid(), "mod", Guid.NewGuid(), "img@sha256:" + new string('a', 64), DummyAssemblyPath(),
            ValidManifest, declaredCapabilities: [], approvedCapabilities: [], priorInstalledAssemblyPath: null,
            versionTransition: (new ModuleVersion(1, 0, 0), new ModuleVersion(1, 5, 0)), versionReapproved: false);

        Assert.True(result.IsTrusted);
    }
}
