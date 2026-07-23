namespace Telechron.Sdk.Modules.Llm;

// R-LLM3: every field call tracking needs to record, returned by the
// engine itself rather than reconstructed by the caller -- token counts
// and model identity are provider-specific response fields, not
// something a generic caller can compute independently.
public sealed record LlmCompletionResult(
    bool Succeeded, string ResponseText, string ModelUsed, int PromptTokens, int CompletionTokens, string? ErrorMessage)
{
    public static LlmCompletionResult Failure(string modelUsed, string error) => new(false, string.Empty, modelUsed, 0, 0, error);
}
