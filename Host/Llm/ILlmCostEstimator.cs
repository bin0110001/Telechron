namespace Telechron.Host.Llm;

// R-LLM3: cost tracking needs a dollar figure even though the engine
// module only returns token counts -- pricing is provider/model-specific
// and changes independently of the engine's request/response logic, so
// it's kept as its own swappable component rather than hardcoded per
// engine module.
public interface ILlmCostEstimator
{
    decimal EstimateCostUsd(string provider, string model, int promptTokens, int completionTokens);
}
