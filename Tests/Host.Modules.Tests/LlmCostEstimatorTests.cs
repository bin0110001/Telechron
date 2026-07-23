using Microsoft.Extensions.Options;
using Telechron.Host.Llm;

namespace Telechron.Host.Modules.Tests;

public class LlmCostEstimatorTests
{
    [Fact]
    public void EstimateCostUsd_NoConfiguredPricing_ReturnsZero()
    {
        var estimator = new LlmCostEstimator(Options.Create(new LlmCostEstimatorOptions()));

        var cost = estimator.EstimateCostUsd("ollama", "gemma4:latest", 1000, 500);

        Assert.Equal(0m, cost);
    }

    [Fact]
    public void EstimateCostUsd_ConfiguredPricing_ComputesCorrectly()
    {
        var estimator = new LlmCostEstimator(Options.Create(new LlmCostEstimatorOptions
        {
            Pricing = { ["openai/gpt-test"] = (PromptPricePerMillion: 10m, CompletionPricePerMillion: 30m) },
        }));

        // 100,000 prompt tokens @ $10/M = $1.00; 50,000 completion @ $30/M = $1.50
        var cost = estimator.EstimateCostUsd("openai", "gpt-test", 100_000, 50_000);

        Assert.Equal(2.50m, cost);
    }

    [Fact]
    public void EstimateCostUsd_DifferentModelSamePricingKeyMismatch_ReturnsZero()
    {
        var estimator = new LlmCostEstimator(Options.Create(new LlmCostEstimatorOptions
        {
            Pricing = { ["openai/gpt-test"] = (10m, 30m) },
        }));

        var cost = estimator.EstimateCostUsd("openai", "different-model", 1000, 1000);

        Assert.Equal(0m, cost);
    }
}
