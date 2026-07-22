namespace Telechron.Sdk.Domain;

// A configurable role packaging prompt/LLM connection/permissions (R-DM6) —
// the single editable home for repair logic, intent planning, synthesis,
// content generation, and agentic operations. Allowlist fields are stored as
// JSON (not real relations) since they're simple permission lists the R-MOD8a
// mediation primitive checks against, not relational data needing joins.
// AllowedSecretHandlesJson holds opaque handles only, per R-SEC1.
public sealed record Persona
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public required string SystemPrompt { get; init; }
    public required string PromptTemplate { get; init; }
    public required Guid LlmConnectionId { get; init; }
    public required string ExecutionMode { get; init; }
    public required string AllowedToolsJson { get; init; }
    public required string AllowedConnectorIdsJson { get; init; }
    public required string AllowedWorkflowIdsJson { get; init; }
    public required int MaxIterations { get; init; }
    public required long MaxLlmCostCents { get; init; }
    public required string ApprovalPolicyJson { get; init; }
    public required string AllowedSecretHandlesJson { get; init; }
}
