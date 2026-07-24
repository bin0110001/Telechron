namespace Telechron.Sdk.Synthesis;

using Telechron.Sdk.Domain;

public sealed record SynthesizedCapabilityResult
{
    public required bool Success { get; init; }
    public required string ModuleName { get; init; }
    public required string FunctionKind { get; init; }
    public required string SourceCode { get; init; }
    public required string SelfTestCode { get; init; }
    public required string Description { get; init; }

    // R-MOD8a: already intersected against the requesting Persona's
    // allowed capabilities by the time this result exists -- never wider
    // than what was actually approved.
    public required IReadOnlyList<string> DeclaredCapabilities { get; init; }
    public string? ErrorMessage { get; init; }

    // Populated once CapabilityVerificationRunner has actually built and
    // self-tested this module in a container (R-SYS6) -- null means "not
    // yet verified," not "assumed passing."
    public string? BuiltAssemblyBlobRef { get; init; }
}

public interface ICapabilitySynthesizer
{
    Task<SynthesizedCapabilityResult> SynthesizeModuleAsync(
        Guid projectId, string missingFunctionKind, DesignDocument? designDocument,
        IReadOnlyList<Requirement> activeRequirements, IReadOnlyList<string> personaAllowedCapabilities,
        CancellationToken ct = default);
}
