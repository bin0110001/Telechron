namespace Telechron.Sdk.Synthesis;

using Telechron.Sdk.Domain;

public sealed record CapabilityVerificationResult
{
    public required bool Success { get; init; }
    public required bool IsEnvironmentFailure { get; init; }
    public required bool HasArchitecturalDrift { get; init; }
    public string? DriftDetails { get; init; }
    public string? OutputMessage { get; init; }
}

public interface ICapabilityVerificationRunner
{
    Task<CapabilityVerificationResult> VerifySynthesizedModuleAsync(
        Guid projectId, SynthesizedCapabilityResult synthesizedModule, DesignDocument? designDocument, CancellationToken ct = default);
}
