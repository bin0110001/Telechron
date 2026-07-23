using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules.Llm;

namespace Telechron.Host.Llm;

// R-LLM1/R-LLM3/R-LLM4/R-MOD8a: the one path an LLM completion is
// requested through. Order: (1) R-LLM4 spend-cap check -- declined before
// any call is dispatched, not noticed after; (2) resolve the provider
// engine (R-LLM1); (3) R-SEC5-shaped secret resolution if the connection
// has one; (4) call the engine, which itself enforces R-LLM5 isolation
// via PromptRenderer; (5) R-LLM3 -- record the call (success or failure)
// regardless of outcome, so a failed call still contributes to spend-cap
// accounting for whatever tokens were actually consumed.
public interface ILlmDispatcher
{
    Task<LlmCompletionResult> DispatchAsync(
        LlmConnection connection, Guid? projectId, LlmCompletionRequest request, CancellationToken ct = default);
}
