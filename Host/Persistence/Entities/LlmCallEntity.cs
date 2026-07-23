namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for LlmCall (R-LLM3).
public sealed class LlmCallEntity
{
    public Guid Id { get; set; }
    public Guid LlmConnectionId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PromptRef { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }

    public LlmConnectionEntity? LlmConnection { get; set; }
    public ProjectEntity? Project { get; set; }
}
