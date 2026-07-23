using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Repair;

// One or more Findings entering the single pipeline together (R-FIX2a: a
// single Finding is the degenerate one-element case of a Repair Plan, not
// a separate code path -- batch aggregation reuses the exact same
// orchestrator, never a bespoke batch pipeline).
public sealed record RepairRequest(
    Guid ProjectId,
    string ProjectRootPath,
    RepairPolicy ProjectPolicy,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<Requirement> ActiveRequirements,
    DesignDocument? DesignDocument);
