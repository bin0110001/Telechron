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
    public string? ErrorMessage { get; init; }
}

public interface ICapabilitySynthesizer
{
    Task<SynthesizedCapabilityResult> SynthesizeModuleAsync(
        Guid projectId, string missingFunctionKind, DesignDocument? designDocument, CancellationToken ct = default);
}
