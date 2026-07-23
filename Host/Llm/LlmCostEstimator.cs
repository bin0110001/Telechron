using Microsoft.Extensions.Options;

namespace Telechron.Host.Llm;

public sealed class LlmCostEstimatorOptions
{
    // "{provider}/{model}" -> (dollarsPerMillionPromptTokens, dollarsPerMillionCompletionTokens).
    // A local/self-hosted provider (Ollama) simply has no entry -- cost is
    // zero, correctly, rather than needing an explicit zero-price row.
    public Dictionary<string, (decimal PromptPricePerMillion, decimal CompletionPricePerMillion)> Pricing { get; set; } = [];
}

public sealed class LlmCostEstimator(IOptions<LlmCostEstimatorOptions> options) : ILlmCostEstimator
{
    public decimal EstimateCostUsd(string provider, string model, int promptTokens, int completionTokens)
    {
        var key = $"{provider}/{model}";
        if (!options.Value.Pricing.TryGetValue(key, out var pricing))
            return 0m;

        return promptTokens / 1_000_000m * pricing.PromptPricePerMillion
            + completionTokens / 1_000_000m * pricing.CompletionPricePerMillion;
    }
}
