using Microsoft.Extensions.Logging;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules.Llm;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Security;

namespace Telechron.Host.Llm;

public sealed class LlmDispatcher(
    ILlmProviderRegistry providerRegistry,
    ISpendCapEnforcer spendCapEnforcer,
    ILlmCostEstimator costEstimator,
    ISecretResolutionScope secretResolutionScope,
    ILlmCallRepository llmCallRepository,
    ILogger<LlmDispatcher> logger) : ILlmDispatcher
{
    public async Task<LlmCompletionResult> DispatchAsync(
        LlmConnection connection, Guid? projectId, LlmCompletionRequest request, CancellationToken ct = default)
    {
        // R-LLM4: checked first -- a call over cap is declined before any
        // provider is even resolved.
        var capCheck = await spendCapEnforcer.CheckAsync(projectId, ct);
        if (!capCheck.IsAllowed)
        {
            logger.LogWarning("LLM call declined for connection '{ConnectionName}': {Reason}", connection.Name, capCheck.Reason);
            return LlmCompletionResult.Failure(string.Empty, capCheck.Reason);
        }

        var engine = providerRegistry.Resolve(connection.Provider);
        if (engine is null)
            return LlmCompletionResult.Failure(string.Empty, $"No engine module registered for provider '{connection.Provider}'.");

        LlmCompletionResult result;
        if (connection.SecretHandle is not null)
        {
            result = await secretResolutionScope.ExecuteAsync(
                connection.SecretHandle,
                secretBytes => engine.CompleteAsync(request, connection.ConfigurationJson, secretBytes, ct),
                ct);
        }
        else
        {
            result = await engine.CompleteAsync(request, connection.ConfigurationJson, ReadOnlyMemory<byte>.Empty, ct);
        }

        // R-LLM3: recorded regardless of success -- a failed call that
        // still consumed prompt tokens (e.g. the provider errored after
        // accepting the request) must still count toward spend tracking.
        var cost = costEstimator.EstimateCostUsd(connection.Provider, result.ModelUsed, result.PromptTokens, result.CompletionTokens);
        await llmCallRepository.AddAsync(new LlmCall
        {
            Id = Guid.NewGuid(),
            LlmConnectionId = connection.Id,
            ProjectId = projectId,
            Provider = connection.Provider,
            Model = result.ModelUsed,
            PromptTokens = result.PromptTokens,
            CompletionTokens = result.CompletionTokens,
            EstimatedCostUsd = cost,
            Succeeded = result.Succeeded,
            ErrorMessage = result.ErrorMessage,
            PromptRef = null,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        }, ct);

        return result;
    }
}
