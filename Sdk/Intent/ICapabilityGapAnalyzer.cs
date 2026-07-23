namespace Telechron.Sdk.Intent;

public sealed record CapabilityGapReport
{
    public required bool HasGaps { get; init; }
    public IReadOnlyList<string> MissingFunctionKinds { get; init; } = [];
    public IReadOnlyList<string> MissingModuleTypes { get; init; } = [];
    public string DetailsJson { get; init; } = "{}";
}

public interface ICapabilityGapAnalyzer
{
    Task<CapabilityGapReport> AnalyzeGapsAsync(string workflowDefinitionJson, CancellationToken ct = default);
}
