using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class LlmCallMapper
{
    public static LlmCall ToDomain(this LlmCallEntity entity) => new()
    {
        Id = entity.Id,
        LlmConnectionId = entity.LlmConnectionId,
        ProjectId = entity.ProjectId,
        Provider = entity.Provider,
        Model = entity.Model,
        PromptTokens = entity.PromptTokens,
        CompletionTokens = entity.CompletionTokens,
        EstimatedCostUsd = entity.EstimatedCostUsd,
        Succeeded = entity.Succeeded,
        ErrorMessage = entity.ErrorMessage,
        PromptRef = entity.PromptRef,
        OccurredAtUtc = entity.OccurredAtUtc,
    };

    public static LlmCallEntity ToEntity(this LlmCall domain) => new()
    {
        Id = domain.Id,
        LlmConnectionId = domain.LlmConnectionId,
        ProjectId = domain.ProjectId,
        Provider = domain.Provider,
        Model = domain.Model,
        PromptTokens = domain.PromptTokens,
        CompletionTokens = domain.CompletionTokens,
        EstimatedCostUsd = domain.EstimatedCostUsd,
        Succeeded = domain.Succeeded,
        ErrorMessage = domain.ErrorMessage,
        PromptRef = domain.PromptRef,
        OccurredAtUtc = domain.OccurredAtUtc,
    };

    public static void ApplyTo(this LlmCall domain, LlmCallEntity entity)
    {
        entity.ErrorMessage = domain.ErrorMessage;
        entity.PromptRef = domain.PromptRef;
    }
}
