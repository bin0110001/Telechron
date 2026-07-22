namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for IntentPlan (R-DM9, R-BUILD1).
public sealed class IntentPlanEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string NaturalLanguageRequest { get; set; } = string.Empty;
    public int PlanningPath { get; set; }
    public string ProposedWorkflowIdsJson { get; set; } = string.Empty;
    public string? CapabilityGapAnalysisJson { get; set; }
    public string? RequiredModulesJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? AppliedAtUtc { get; set; }

    public ProjectEntity? Project { get; set; }
}
