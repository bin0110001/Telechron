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
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
