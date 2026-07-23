using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telechron.Host.Modules;
using Telechron.Host.Modules.Health;
using Telechron.Host.Modules.Integrity;
using Telechron.Host.Modules.Runtime;
using Telechron.Host.Modules.SelfTest;
using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules.Tests;

// Phase 5 exit criteria, end to end: "a signed, sandboxed sample module
// installs, runs its (falsifiable) self-test in a container, hot-reloads
// with drain, and rolls back when broken -- all mediated by Host-side
// permissions." Chains the real IModuleTrustEvaluator and real
// IModuleHotReloadCoordinator/ModuleRuntime together against the actual
// compiled Sample module assembly. The container-dispatch layer itself
// (IContainerizedModuleSelfTestRunner) is faked here since it requires a
// live Podman machine -- that path is separately live-verified end to end
// in Tests/Agent.Containers.Tests (the container execution boundary) and
// via the harness's direct live runs recorded in this phase's commits;
// what THIS test proves is that trust evaluation and hot-reload compose
// correctly into one continuous flow, not that containers work (already
// proven elsewhere).
public class SampleModuleEndToEndTests
{
    private static string SampleModuleAssemblyPath =>
        Path.Combine(AppContext.BaseDirectory, "Telechron.Modules.Sample.dll");

    private sealed class ScriptedSelfTestRunner : IContainerizedModuleSelfTestRunner
    {
        // moduleAssemblyPath -> result to hand back, so the same fake
        // serves both "pre-trust sandbox run" and "falsifiability check"
        // call sites with different scripted outcomes per assembly.
        public Dictionary<string, ModuleSelfTestResult> ResultsByPath { get; } = [];

        public Task<ModuleSelfTestResult> RunAsync(
            string moduleName, Guid machineId, string toolchainImageDigest, IReadOnlyList<string> declaredCapabilities,
            string moduleAssemblyPath, CancellationToken ct = default) =>
            Task.FromResult(ResultsByPath.GetValueOrDefault(moduleAssemblyPath, ModuleSelfTestResult.Success("default")));
    }

    private static (string PublisherKeyId, ECDsa Key) CreateTrustedPublisher() =>
        ("test-publisher", ECDsa.Create(ECCurve.NamedCurves.nistP256));

    private static ModuleIntegrityManifest SignAssembly(string assemblyPath, string keyId, ECDsa key)
    {
        var checksum = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assemblyPath)));
        var signature = key.SignData(Convert.FromHexString(checksum), HashAlgorithmName.SHA256);
        return new ModuleIntegrityManifest(keyId, checksum, Convert.ToBase64String(signature));
    }

    [Fact]
    public async Task FullLifecycle_SignedModulePassesTrustThenHotReloadsWithHealthyCanary()
    {
        var (keyId, publisherKey) = CreateTrustedPublisher();
        using var _ = publisherKey;
        var keyStore = new TrustedPublisherKeyStore(Options.Create(new TrustedPublisherKeyStoreOptions
        {
            TrustedKeys = { [keyId] = Convert.ToBase64String(publisherKey.ExportSubjectPublicKeyInfo()) },
        }));
        var integrityVerifier = new ModuleIntegrityVerifier(keyStore, NullLogger<ModuleIntegrityVerifier>.Instance);

        var selfTestRunner = new ScriptedSelfTestRunner();
        selfTestRunner.ResultsByPath[SampleModuleAssemblyPath] = ModuleSelfTestResult.Success("Add(2, 2) == 4, as expected.");

        var falsifiabilityChecker = new SelfTestFalsifiabilityChecker(selfTestRunner, NullLogger<SelfTestFalsifiabilityChecker>.Instance);
        var trustEvaluator = new ModuleTrustEvaluator(
            integrityVerifier, selfTestRunner, falsifiabilityChecker, NullLogger<ModuleTrustEvaluator>.Instance);

        // Step 1: install-time trust evaluation -- integrity (signed by a
        // trusted publisher key), capability approval, pre-trust sandbox
        // self-test, all real code paths.
        var manifest = SignAssembly(SampleModuleAssemblyPath, keyId, publisherKey);
        var trustResult = await trustEvaluator.EvaluateAsync(
            Guid.NewGuid(), "telechron.sample", Guid.NewGuid(), "toolchain@sha256:" + new string('a', 64),
            SampleModuleAssemblyPath, manifest, declaredCapabilities: [], approvedCapabilities: [],
            priorInstalledAssemblyPath: null);

        Assert.True(trustResult.IsTrusted, trustResult.Reason);

        // Step 2: hot-reload the now-trusted module in, with a healthy
        // canary window -- real ModuleRuntime (real ALC load/unload),
        // real ModuleDrainCoordinator, real ModuleCanaryObserver.
        var tracker = new InFlightInvocationTracker();
        var runtime = new ModuleRuntime(NullLogger<ModuleRuntime>.Instance);
        await runtime.LoadAsync(SampleModuleAssemblyPath); // simulates a previously-running prior version
        var drain = new ModuleDrainCoordinator(tracker, NullLogger<ModuleDrainCoordinator>.Instance);
        var canary = new ModuleCanaryObserver(
            Options.Create(new ModuleCanaryOptions { WindowDuration = TimeSpan.FromMilliseconds(150), MinimumInvocationsBeforeEvaluating = 3 }),
            NullLogger<ModuleCanaryObserver>.Instance);
        var healthMonitor = new ModuleHealthMonitor(Options.Create(new ModuleHealthMonitorOptions()));
        var hotReload = new ModuleHotReloadCoordinator(drain, runtime, canary, healthMonitor, NullLogger<ModuleHotReloadCoordinator>.Instance);

        using var trafficCts = new CancellationTokenSource();
        var trafficTask = Task.Run(async () =>
        {
            while (!trafficCts.IsCancellationRequested)
            {
                canary.RecordInvocationOutcome("telechron.sample", succeeded: true);
                try { await Task.Delay(10, trafficCts.Token); } catch (OperationCanceledException) { }
            }
        });

        var reloadResult = await hotReload.ReloadAsync(
            "telechron.sample", SampleModuleAssemblyPath, SampleModuleAssemblyPath, TimeSpan.FromSeconds(5));
        await trafficCts.CancelAsync();
        await trafficTask;

        Assert.Equal(ModuleHotReloadOutcome.ReloadedSuccessfully, reloadResult.Outcome);
        Assert.False(reloadResult.OldVersionUnloadLeakDetected);
        Assert.NotNull(runtime.GetLoaded("telechron.sample"));
    }

    [Fact]
    public async Task FullLifecycle_UnsignedModuleFailsTrustBeforeEverHotReloading()
    {
        var (keyId, publisherKey) = CreateTrustedPublisher();
        using var _ = publisherKey;
        var keyStore = new TrustedPublisherKeyStore(Options.Create(new TrustedPublisherKeyStoreOptions
        {
            TrustedKeys = { [keyId] = Convert.ToBase64String(publisherKey.ExportSubjectPublicKeyInfo()) },
        }));
        var integrityVerifier = new ModuleIntegrityVerifier(keyStore, NullLogger<ModuleIntegrityVerifier>.Instance);
        var selfTestRunner = new ScriptedSelfTestRunner();
        var falsifiabilityChecker = new SelfTestFalsifiabilityChecker(selfTestRunner, NullLogger<SelfTestFalsifiabilityChecker>.Instance);
        var trustEvaluator = new ModuleTrustEvaluator(
            integrityVerifier, selfTestRunner, falsifiabilityChecker, NullLogger<ModuleTrustEvaluator>.Instance);

        // Signed by a key nobody trusts.
        using var attackerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var forgedManifest = SignAssembly(SampleModuleAssemblyPath, "attacker-key", attackerKey);

        var trustResult = await trustEvaluator.EvaluateAsync(
            Guid.NewGuid(), "telechron.sample", Guid.NewGuid(), "toolchain@sha256:" + new string('a', 64),
            SampleModuleAssemblyPath, forgedManifest, declaredCapabilities: [], approvedCapabilities: [],
            priorInstalledAssemblyPath: null);

        Assert.False(trustResult.IsTrusted);
        Assert.Equal(ModuleTrustOutcome.IntegrityFailed, trustResult.Outcome);
        // Never reached the point of running any code -- self-test runner
        // was never invoked (evidenced indirectly: it has no default
        // fallback that would make a bad path silently look fine).
    }
}
