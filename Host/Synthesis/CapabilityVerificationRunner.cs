namespace Telechron.Host.Synthesis;

using System.IO.Compression;
using System.Text.Json;
using Telechron.Host.Agents.Dispatch;
using Telechron.Host.Llm;
using Telechron.Host.Modules;
using Telechron.Host.Repair;
using Telechron.Sdk.Agents;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Repair;
using Telechron.Sdk.Synthesis;

// R-BUILD3/R-FIX13/R-MOD5a/R-MOD5b: verifies a synthesized module for
// real -- builds it (and its self-test) inside a container via the same
// dispatch pattern as the repair pipeline's Verify stage, signs the
// resulting assembly with Telechron's own synthesis integrity key, then
// routes it through the exact same IModuleTrustEvaluator pre-trust
// pipeline every other module install uses (R-ENG4: no parallel trust
// path for "our own" synthesized code), and checks architectural drift
// against the Project's REAL active Requirements, never an empty stand-in.
// ArchitecturalDriftDetector needs a Project-specific LlmConnection (same
// reasoning as CapabilitySynthesizer/LlmFixGenerator), so it's constructed
// per-call here rather than injected -- llmConnectionRepository resolves
// whichever connection the caller's Project uses.
public sealed class CapabilityVerificationRunner(
    IArtifactBlobStore blobStore,
    IDispatchQueue dispatchQueue,
    ICommandResultCorrelator resultCorrelator,
    SynthesisIntegritySigner integritySigner,
    IModuleTrustEvaluator trustEvaluator,
    ILlmDispatcher llmDispatcher,
    ILlmConnectionRepository llmConnectionRepository,
    IProjectRepository projectRepository) : ICapabilityVerificationRunner
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(10);

    // Same pinned .NET SDK image DotnetToolchainModule uses -- synthesized
    // modules are always C#/.NET targeting this solution's runtime.
    private const string DotnetSdkImageDigest = "mcr.microsoft.com/dotnet/sdk@sha256:72b2c1fba104eed0765e76c66256dd57b8b00c5e7c7fd16ad3eb254ad18db3fc";

    public async Task<CapabilityVerificationResult> VerifySynthesizedModuleAsync(
        Guid projectId, SynthesizedCapabilityResult synthesizedModule, DesignDocument? designDocument,
        Guid machineId, IReadOnlyList<Requirement> activeRequirements, IReadOnlyList<string> approvedCapabilities,
        CancellationToken ct = default)
    {
        if (!synthesizedModule.Success)
        {
            return new CapabilityVerificationResult
            {
                Success = false,
                IsEnvironmentFailure = false,
                HasArchitecturalDrift = false,
                OutputMessage = $"Synthesis failed prior to verification: {synthesizedModule.ErrorMessage}"
            };
        }

        var project = await projectRepository.GetByIdAsync(projectId, ct)
            ?? throw new InvalidOperationException($"Project '{projectId}' does not exist.");
        if (project.LlmConnectionId is not { } llmConnectionId)
            throw new InvalidOperationException($"Project '{projectId}' has no LlmConnection assigned -- cannot run architectural drift detection.");
        var llmConnection = await llmConnectionRepository.GetByIdAsync(llmConnectionId, ct)
            ?? throw new InvalidOperationException($"LlmConnection '{llmConnectionId}' referenced by Project '{projectId}' no longer exists.");
        var driftDetector = new ArchitecturalDriftDetector(llmDispatcher, llmConnection);

        // R-FIX13/R-BUILD3: drift is checked against the REAL active
        // Requirements the caller supplied, before any build/container
        // cost is spent -- a synthesized capability that contradicts an
        // Active Requirement is rejected regardless of whether it would
        // otherwise compile and pass its self-test.
        var fileChange = new PatchFileChange($"{synthesizedModule.ModuleName}.cs", synthesizedModule.SourceCode);
        var patch = new PatchDiff([fileChange]);
        var driftReport = await driftDetector.CheckAsync(patch, activeRequirements, ct);
        if (driftReport.IsDrift)
        {
            return new CapabilityVerificationResult
            {
                Success = false,
                IsEnvironmentFailure = false,
                HasArchitecturalDrift = true,
                DriftDetails = driftReport.Reason,
                OutputMessage = $"Verification flagged architectural drift against active requirements: {driftReport.Reason}"
            };
        }

        // Build the synthesized module + self-test in a real container.
        string builtAssemblyBlobRef;
        try
        {
            builtAssemblyBlobRef = await BuildInContainerAsync(synthesizedModule, machineId, ct);
        }
        catch (InvalidOperationException ex)
        {
            return new CapabilityVerificationResult
            {
                Success = false, IsEnvironmentFailure = false, HasArchitecturalDrift = false,
                OutputMessage = $"Synthesis build failed: {ex.Message}",
            };
        }

        // R-MOD5a/R-MOD5b: route through the real pre-trust pipeline
        // against the real compiled assembly -- same evaluator every
        // other module install uses, no Host-synthesized-code shortcut.
        var localAssemblyPath = Path.Combine(Path.GetTempPath(), $"telechron-synthesis-verify-{Guid.NewGuid():N}.dll");
        try
        {
            await using (var blobStream = await blobStore.OpenReadAsync(builtAssemblyBlobRef, ct))
            await using (var fileStream = File.Create(localAssemblyPath))
            {
                await blobStream.CopyToAsync(fileStream, ct);
            }

            var assemblyBytes = await File.ReadAllBytesAsync(localAssemblyPath, ct);
            var manifest = integritySigner.Sign(assemblyBytes);

            var trustResult = await trustEvaluator.EvaluateAsync(
                projectId, synthesizedModule.ModuleName, machineId, DotnetSdkImageDigest, localAssemblyPath,
                manifest, synthesizedModule.DeclaredCapabilities, approvedCapabilities,
                priorInstalledAssemblyPath: null, ct: ct);

            return new CapabilityVerificationResult
            {
                Success = trustResult.IsTrusted,
                IsEnvironmentFailure = false,
                HasArchitecturalDrift = false,
                OutputMessage = trustResult.IsTrusted
                    ? $"Synthesized module '{synthesizedModule.ModuleName}' passed the real pre-trust pipeline (build, self-test, capability approval, sandbox)."
                    : $"Synthesized module '{synthesizedModule.ModuleName}' failed pre-trust evaluation: {trustResult.Reason}",
                BuiltAssemblyBlobRef = trustResult.IsTrusted ? builtAssemblyBlobRef : null,
            };
        }
        finally
        {
            try { File.Delete(localAssemblyPath); } catch (IOException) { /* best-effort cleanup */ }
        }
    }

    private async Task<string> BuildInContainerAsync(SynthesizedCapabilityResult synthesizedModule, Guid machineId, CancellationToken ct)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), $"telechron-synthesis-bundle-{Guid.NewGuid():N}.zip");
        try
        {
            SynthesisBundleBuilder.WriteBundleZip(zipPath, synthesizedModule);

            string blobRef;
            await using (var zipStream = File.OpenRead(zipPath))
            {
                blobRef = await blobStore.SaveAsync(zipStream, "synthesis-bundle.zip", ct);
            }

            var commandId = Guid.NewGuid();
            var parametersJson = JsonSerializer.Serialize(new
            {
                sourceBundleBlobRef = blobRef,
                toolchainImageDigest = DotnetSdkImageDigest,
                moduleName = synthesizedModule.ModuleName,
            });

            var outcome = await resultCorrelator.AwaitResultAsync(
                commandId,
                dispatch: () =>
                {
                    var validation = dispatchQueue.Enqueue(machineId, new DispatchedCommand(
                        commandId, RunId: Guid.Empty, CommandKinds.RunCapabilitySynthesisBuild, parametersJson, DotnetSdkImageDigest));
                    if (!validation.IsValid)
                        throw new InvalidOperationException($"Synthesis build dispatch rejected: {string.Join("; ", validation.Errors)}");
                    return Task.CompletedTask;
                },
                BuildTimeout, ct);

            if (!outcome.Succeeded)
                throw new InvalidOperationException(outcome.ErrorMessage);

            // RunCapabilitySynthesisBuildCommandHandler returns the built
            // assembly's Host blob ref as its success summary.
            return outcome.OutputSummary;
        }
        finally
        {
            try { File.Delete(zipPath); } catch (IOException) { /* best-effort cleanup */ }
        }
    }
}
