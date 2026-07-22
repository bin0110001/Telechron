using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class PersonaMapper
{
    public static Persona ToDomain(this PersonaEntity entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        Name = entity.Name,
        SystemPrompt = entity.SystemPrompt,
        PromptTemplate = entity.PromptTemplate,
        LlmConnectionId = entity.LlmConnectionId,
        ExecutionMode = entity.ExecutionMode,
        AllowedToolsJson = entity.AllowedToolsJson,
        AllowedConnectorIdsJson = entity.AllowedConnectorIdsJson,
        AllowedWorkflowIdsJson = entity.AllowedWorkflowIdsJson,
        MaxIterations = entity.MaxIterations,
        MaxLlmCostCents = entity.MaxLlmCostCents,
        ApprovalPolicyJson = entity.ApprovalPolicyJson,
        AllowedSecretHandlesJson = entity.AllowedSecretHandlesJson,
    };

    public static PersonaEntity ToEntity(this Persona domain) => new()
    {
        Id = domain.Id,
        ProjectId = domain.ProjectId,
        Name = domain.Name,
        SystemPrompt = domain.SystemPrompt,
        PromptTemplate = domain.PromptTemplate,
        LlmConnectionId = domain.LlmConnectionId,
        ExecutionMode = domain.ExecutionMode,
        AllowedToolsJson = domain.AllowedToolsJson,
        AllowedConnectorIdsJson = domain.AllowedConnectorIdsJson,
        AllowedWorkflowIdsJson = domain.AllowedWorkflowIdsJson,
        MaxIterations = domain.MaxIterations,
        MaxLlmCostCents = domain.MaxLlmCostCents,
        ApprovalPolicyJson = domain.ApprovalPolicyJson,
        AllowedSecretHandlesJson = domain.AllowedSecretHandlesJson,
    };

    public static void ApplyTo(this Persona domain, PersonaEntity entity)
    {
        entity.Name = domain.Name;
        entity.SystemPrompt = domain.SystemPrompt;
        entity.PromptTemplate = domain.PromptTemplate;
        entity.LlmConnectionId = domain.LlmConnectionId;
        entity.ExecutionMode = domain.ExecutionMode;
        entity.AllowedToolsJson = domain.AllowedToolsJson;
        entity.AllowedConnectorIdsJson = domain.AllowedConnectorIdsJson;
        entity.AllowedWorkflowIdsJson = domain.AllowedWorkflowIdsJson;
        entity.MaxIterations = domain.MaxIterations;
        entity.MaxLlmCostCents = domain.MaxLlmCostCents;
        entity.ApprovalPolicyJson = domain.ApprovalPolicyJson;
        entity.AllowedSecretHandlesJson = domain.AllowedSecretHandlesJson;
    }
}
