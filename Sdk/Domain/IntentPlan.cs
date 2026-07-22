namespace Telechron.Sdk.Domain;

// A side-effect-free proposal converting NL into workflows/gap-analysis
// (R-DM9). PlanningPath records which mechanism produced it (R-BUILD1).
// AppliedAtUtc null means not yet applied — "Applying a plan is an explicit
// operation" (R-BUILD2: plans themselves never mutate state).
public sealed record IntentPlan
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required string NaturalLanguageRequest { get; init; }
    public required IntentPlanningPath PlanningPath { get; init; }
    public required string ProposedWorkflowIdsJson { get; init; }
    public string? CapabilityGapAnalysisJson { get; init; }
    public string? RequiredModulesJson { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? AppliedAtUtc { get; init; }
}
