namespace Telechron.Sdk.Synthesis;

using Telechron.Sdk.Domain;

public sealed record CapabilityVerificationResult
{
    public required bool Success { get; init; }
    public required bool IsEnvironmentFailure { get; init; }
    public required bool HasArchitecturalDrift { get; init; }
    public string? DriftDetails { get; init; }
    public string? OutputMessage { get; init; }

    // The Host blob ref of the real, built, pre-trust-passed assembly --
    // populated only when Success is true, so IModuleRuntime.LoadAsync has
    // something real to load rather than needing to recompile.
    public string? BuiltAssemblyBlobRef { get; init; }
}

public interface ICapabilityVerificationRunner
{
    Task<CapabilityVerificationResult> VerifySynthesizedModuleAsync(
        Guid projectId, SynthesizedCapabilityResult synthesizedModule, DesignDocument? designDocument,
        Guid machineId, IReadOnlyList<Requirement> activeRequirements, IReadOnlyList<string> approvedCapabilities,
        CancellationToken ct = default);
}
