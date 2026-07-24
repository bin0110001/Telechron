namespace Telechron.Host.Storefront.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telechron.Host.Acceptance;
using Telechron.Host.Modules;
using Telechron.Host.Modules.Runtime;
using Telechron.Host.Storefront;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Storefront;
using Telechron.Sdk.Workflows;

public sealed class StorefrontAndAcceptanceTests
{
    private static StorefrontModuleListing SampleListing() => new()
    {
        ModuleId = "mod_sample",
        Name = "Sample Module",
        Version = "1.0.0",
        Description = "Sample",
        PublisherId = "pub_1",
        PublisherKeyId = "key_1",
        PackageSha256Checksum = "checksum",
        SignatureBase64 = "sig",
        RequestedCapabilities = [],
        DownloadUrl = "http://example.com"
    };

    private sealed class StubDownloader : IStorefrontPackageDownloader
    {
        public Task<string> DownloadToTempFileAsync(StorefrontModuleListing listing, CancellationToken ct = default)
        {
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, [1, 2, 3]);
            return Task.FromResult(path);
        }
    }

    private sealed class StubIntegrityVerifier(bool isValid) : IModuleIntegrityVerifier
    {
        public Task<IntegrityVerificationResult> VerifyAsync(string path, ModuleIntegrityManifest manifest, CancellationToken ct = default) =>
            Task.FromResult(new IntegrityVerificationResult(isValid, isValid ? "ok" : "checksum/signature mismatch"));
    }

    private sealed class StubTrustEvaluator(ModuleTrustOutcome outcome) : IModuleTrustEvaluator
    {
        public Task<ModuleTrustResult> EvaluateAsync(
            Guid projectId, string moduleName, Guid machineId, string toolchainImageDigest, string candidateAssemblyPath,
            ModuleIntegrityManifest integrityManifest, IReadOnlyList<string> declaredCapabilities,
            IReadOnlyList<string> approvedCapabilities, string? priorInstalledAssemblyPath,
            (ModuleVersion Installed, ModuleVersion Candidate)? versionTransition = null, bool versionReapproved = false,
            CancellationToken ct = default) =>
            Task.FromResult(new ModuleTrustResult(outcome, outcome == ModuleTrustOutcome.Trusted ? "trusted" : "sandbox rejected"));
    }

    private sealed class StubModuleRuntime : IModuleRuntime
    {
        public Task<LoadedModule> LoadAsync(string moduleAssemblyPath, CancellationToken ct = default) =>
            Task.FromResult(new LoadedModule
            {
                ModuleName = "mod_sample",
                Version = new ModuleVersion(1, 0, 0),
                Instance = null!,
                LoadContext = null!,
                UnloadWeakReference = new WeakReference(new object()),
                LoadedAtUtc = DateTimeOffset.UtcNow,
            });

        public LoadedModule? GetLoaded(string moduleName) => null;
        public TModule? GetLoadedAs<TModule>(string moduleName) where TModule : class, IModule => null;
        public Task<ModuleUnloadResult> UnloadAsync(string moduleName, CancellationToken ct = default) =>
            Task.FromResult(new ModuleUnloadResult(true, false));
    }

    private static StorefrontAcquisitionContext SampleContext() => new()
    {
        TargetProjectId = Guid.NewGuid(),
        TargetMachineId = Guid.NewGuid(),
        ToolchainImageDigest = "img@sha256:" + new string('a', 64),
        ProjectApprovedCapabilities = [],
    };

    private static StorefrontCatalogService BuildService(
        bool enabled, bool requireSignature, bool enforceSandbox,
        bool integrityValid, ModuleTrustOutcome trustOutcome) =>
        new(
            Options.Create(new StorefrontOptions { Enabled = enabled, RequirePublisherSignature = requireSignature, EnforcePreTrustContainerSandbox = enforceSandbox }),
            new StubDownloader(),
            new StubIntegrityVerifier(integrityValid),
            new StubTrustEvaluator(trustOutcome),
            new StubModuleRuntime(),
            NullLogger<StorefrontCatalogService>.Instance);

    [Fact]
    public async Task StorefrontService_DisabledByDefault_RejectsAcquisition()
    {
        var service = BuildService(enabled: false, requireSignature: true, enforceSandbox: true, integrityValid: true, trustOutcome: ModuleTrustOutcome.Trusted);

        var result = await service.AcquireAndInstallModuleAsync(SampleListing(), SampleContext());

        Assert.False(result.Success);
        Assert.Contains("disabled by default", result.ErrorMessage);
    }

    [Fact]
    public async Task StorefrontService_EnabledWithValidSignatureAndSandbox_Succeeds()
    {
        var service = BuildService(enabled: true, requireSignature: true, enforceSandbox: true, integrityValid: true, trustOutcome: ModuleTrustOutcome.Trusted);

        var result = await service.AcquireAndInstallModuleAsync(SampleListing(), SampleContext());

        Assert.True(result.Success);
        Assert.True(result.SignatureVerified);
        Assert.True(result.ContainerSandboxPassed);
        Assert.NotNull(result.InstalledModulePath);
    }

    // R-MOD5a: a listing whose checksum/signature does not verify must be
    // rejected outright -- this is the real negative path the original
    // implementation never exercised (its "verification" only checked the
    // fields were non-empty, so it could never fail).
    [Fact]
    public async Task StorefrontService_InvalidSignature_RejectsAcquisitionWithoutRunningSandbox()
    {
        var service = BuildService(enabled: true, requireSignature: true, enforceSandbox: true, integrityValid: false, trustOutcome: ModuleTrustOutcome.Trusted);

        var result = await service.AcquireAndInstallModuleAsync(SampleListing(), SampleContext());

        Assert.False(result.Success);
        Assert.False(result.SignatureVerified);
        Assert.False(result.ContainerSandboxPassed);
        Assert.Contains("R-MOD5a", result.ErrorMessage);
    }

    // R-MOD5b: a package that passes integrity but fails the real pre-trust
    // sandboxed self-test/capability-approval pipeline must be rejected --
    // the original implementation hardcoded sandboxPassed = true and could
    // never reach this branch.
    [Fact]
    public async Task StorefrontService_SandboxSelfTestFails_RejectsAcquisition()
    {
        var service = BuildService(enabled: true, requireSignature: true, enforceSandbox: true, integrityValid: true, trustOutcome: ModuleTrustOutcome.PreTrustSelfTestFailed);

        var result = await service.AcquireAndInstallModuleAsync(SampleListing(), SampleContext());

        Assert.False(result.Success);
        Assert.True(result.SignatureVerified);
        Assert.False(result.ContainerSandboxPassed);
        Assert.Contains("R-MOD5b", result.ErrorMessage);
    }

    // R-MOD8, surfaced through the same trust evaluator: a module declaring
    // a capability the Project hasn't approved is rejected before any
    // sandboxed code runs.
    [Fact]
    public async Task StorefrontService_CapabilityNotApproved_RejectsAcquisition()
    {
        var service = BuildService(enabled: true, requireSignature: true, enforceSandbox: true, integrityValid: true, trustOutcome: ModuleTrustOutcome.CapabilityNotApproved);

        var result = await service.AcquireAndInstallModuleAsync(SampleListing(), SampleContext());

        Assert.False(result.Success);
        Assert.False(result.ContainerSandboxPassed);
    }

    // The old verifier could never fail (every gate was a hardcoded `true`
    // literal with zero dependencies). The real one reads DI registrations,
    // so an empty container -- nothing registered -- must report real
    // failures, proving the gates are wired to something checkable rather
    // than decorative.
    [Fact]
    public void SolutionAcceptanceVerifier_EmptyServiceProvider_ReportsRealFailures()
    {
        var emptyProvider = new ServiceCollection().BuildServiceProvider();
        var verifier = new SolutionAcceptanceVerifier(emptyProvider);

        var report = verifier.EvaluateSection9AcceptanceGates();

        Assert.Equal(10, report.GateResults.Count);
        Assert.False(report.AllGatesPassed);
        // GATE_10 (frontend parity) is explicitly out of the Host's
        // in-process reach -- see its Description -- everything else
        // that depends on a resolvable Host service must fail here.
        Assert.All(
            report.GateResults.Where(g => g.GateId != "GATE_10"),
            g => Assert.False(g.Passed));
    }

    // Proves gates flip to passing once their real service is actually
    // resolvable -- using IWorkflowEngine (GATE_8) since it has a simple
    // interface a hand-rolled stub can implement, unlike RepairPipelineOrchestrator's
    // large concrete constructor graph (that class's real wiring is proven
    // by Host.Repair.Tests instead).
    private sealed class StubWorkflowEngine : IWorkflowEngine
    {
        private static Task<WorkflowRun> NotExercised() =>
            throw new NotSupportedException("Not exercised by this test -- only GetService<IWorkflowEngine>() resolution is under test.");

        public Task<WorkflowRun> StartWorkflowAsync(Guid workflowId, Dictionary<string, string>? inputVariables = null, CancellationToken ct = default) => NotExercised();
        public Task<WorkflowRun> ExecuteRunAsync(Guid workflowRunId, CancellationToken ct = default) => NotExercised();
        public Task<WorkflowRun> ResumeRunAsync(Guid workflowRunId, Guid approvalRequestId, CancellationToken ct = default) => NotExercised();
        public Task<WorkflowRun> CancelRunAsync(Guid workflowRunId, string reason, CancellationToken ct = default) => NotExercised();
    }

    [Fact]
    public void SolutionAcceptanceVerifier_WorkflowEngineRegistered_Gate8Passes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowEngine>(new StubWorkflowEngine());
        var verifier = new SolutionAcceptanceVerifier(services.BuildServiceProvider());

        var report = verifier.EvaluateSection9AcceptanceGates();

        Assert.True(report.GateResults.Single(g => g.GateId == "GATE_8").Passed);
    }
}
