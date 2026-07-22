namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for Persona (R-DM6).
public sealed class PersonaEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string PromptTemplate { get; set; } = string.Empty;
    public Guid LlmConnectionId { get; set; }
    public string ExecutionMode { get; set; } = string.Empty;
    public string AllowedToolsJson { get; set; } = string.Empty;
    public string AllowedConnectorIdsJson { get; set; } = string.Empty;
    public string AllowedWorkflowIdsJson { get; set; } = string.Empty;
    public int MaxIterations { get; set; }
    public long MaxLlmCostCents { get; set; }
    public string ApprovalPolicyJson { get; set; } = string.Empty;
    public string AllowedSecretHandlesJson { get; set; } = string.Empty;

    public ProjectEntity? Project { get; set; }
    public LlmConnectionEntity? LlmConnection { get; set; }
}
