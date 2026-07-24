namespace Telechron.Sdk.Domain;

// The top-level configuration aggregate and unit of isolation/trust/scheduling
// (R-DM1). FKs to Runs, Workflows, Connectors, etc. are added in Phase 3 once
// those entities exist — this Phase 1 shape carries only what RBAC and the
// secret seams need: identity, root path, owner, and Repair Policy.
public sealed class Project
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string RootPath { get; init; }
    public required Guid OwnerUserId { get; init; }
    public required RepairPolicy RepairPolicy { get; init; }

    // The Toolchain the repair pipeline's Verify stage (R-FIX2) builds/tests
    // this Project against. Nullable: a Project can exist before a
    // Toolchain is assigned, and repair-candidate promotion for such a
    // Project simply has no Verify path yet (R-FIX8's Environment-vs-Code
    // classification already routes around anything that can't be
    // meaningfully re-verified).
    public Guid? ToolchainId { get; init; }

    // The LlmConnection Generate Fix (R-FIX2/R-DM6a) and the R-FIX13 drift
    // detector call through for this Project. Nullable for the same reason
    // as ToolchainId -- a Project without one has no LLM-backed repair path
    // yet, only the deterministic-fix path (R-FIX5).
    public Guid? LlmConnectionId { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
}
