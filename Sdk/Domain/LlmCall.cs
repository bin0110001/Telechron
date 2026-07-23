namespace Telechron.Sdk.Domain;

// R-LLM3: "Every call records provider/model/tokens/cost/prompt
// metadata and surfaces in UI." One row per completion call. PromptRef
// points outside SQLite (same out-of-DB pattern as Artifact/Module blobs,
// R-PER7) -- the full rendered prompt (including untrusted content
// blocks) can be large and isn't queried relationally, only retrieved for
// audit/debugging.
public sealed record LlmCall
{
    public required Guid Id { get; init; }
    public required Guid LlmConnectionId { get; init; }
    public Guid? ProjectId { get; init; }
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public required int PromptTokens { get; init; }
    public required int CompletionTokens { get; init; }
    public required decimal EstimatedCostUsd { get; init; }
    public required bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public string? PromptRef { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
}
