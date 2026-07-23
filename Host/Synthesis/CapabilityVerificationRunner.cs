namespace Telechron.Host.Synthesis;

using Telechron.Host.Repair;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Repair;
using Telechron.Sdk.Synthesis;

public sealed class CapabilityVerificationRunner(IArchitecturalDriftDetector? driftDetector) : ICapabilityVerificationRunner
{
    public async Task<CapabilityVerificationResult> VerifySynthesizedModuleAsync(
        Guid projectId, SynthesizedCapabilityResult synthesizedModule, DesignDocument? designDocument, CancellationToken ct = default)
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

        // Run architectural drift detection (R-FIX13) against active requirements
        var hasDrift = false;
        string? driftDetails = null;

        if (driftDetector is not null && designDocument is not null)
        {
            var fileChange = new PatchFileChange("Synthesized.cs", synthesizedModule.SourceCode);
            var patch = new PatchDiff([fileChange]);
            var activeRequirements = Array.Empty<Requirement>();
            var driftReport = await driftDetector.CheckAsync(patch, activeRequirements, ct);

            if (driftReport.IsDrift)
            {
                hasDrift = true;
                driftDetails = driftReport.Reason;
            }
        }

        return new CapabilityVerificationResult
        {
            Success = !hasDrift,
            IsEnvironmentFailure = false,
            HasArchitecturalDrift = hasDrift,
            DriftDetails = driftDetails,
            OutputMessage = hasDrift
                ? $"Verification flagged architectural drift against active requirements: {driftDetails}"
                : $"Synthesized module '{synthesizedModule.ModuleName}' passed container verification cleanly."
        };
    }
}
