namespace Telechron.Host.DesignDocs;

using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Workflows.Approvals;

public sealed record ProposedRevisionRequest
{
    public required Guid Id { get; init; }
    public required Guid RequirementId { get; init; }
    public required string ProposedTitle { get; init; }
    public required string ProposedBody { get; init; }
    public required string ChangeReason { get; init; }
    public required Guid ProposedByUserId { get; init; }
    public required Guid ApprovalRequestId { get; init; }
    public required bool Approved { get; init; }
}

public sealed class DesignDocumentManager(
    IDesignDocumentRepository designDocRepo,
    IRequirementRepository requirementRepo,
    IRequirementRevisionRepository revisionRepo,
    IApprovalManager approvalManager)
{
    public async Task<ProposedRevisionRequest> ProposeRevisionAsync(
        Guid requirementId, string proposedTitle, string proposedBody, string changeReason, Guid userId, CancellationToken ct = default)
    {
        var req = await requirementRepo.GetByIdAsync(requirementId, ct)
            ?? throw new KeyNotFoundException($"Requirement '{requirementId}' was not found.");

        _ = await designDocRepo.GetByIdAsync(req.DesignDocumentId, ct);

        // R-DM16b / R-SEC4: Design document revisions route through the privileged-path human approval gate
        var prompt = $"Proposed revision for Requirement '{req.RequirementId}' ({req.Title}): '{changeReason}'. Approve revision?";
        var approvalRequest = await approvalManager.CreateRequestAsync(
            requirementId, "requirement-revision", "R-DM16b-privileged-path", prompt, ct);

        return new ProposedRevisionRequest
        {
            Id = Guid.NewGuid(),
            RequirementId = requirementId,
            ProposedTitle = proposedTitle,
            ProposedBody = proposedBody,
            ChangeReason = changeReason,
            ProposedByUserId = userId,
            ApprovalRequestId = approvalRequest.Id,
            Approved = false
        };
    }

    public async Task<RequirementRevision> ApplyApprovedRevisionAsync(
        ProposedRevisionRequest proposal, Guid approverUserId, CancellationToken ct = default)
    {
        var approvalRequest = await approvalManager.GetRequestByIdAsync(proposal.ApprovalRequestId, ct)
            ?? throw new KeyNotFoundException($"Approval request '{proposal.ApprovalRequestId}' was not found.");

        if (!approvalRequest.IsSatisfied)
        {
            throw new InvalidOperationException($"Design Document revision for requirement '{proposal.RequirementId}' has not been approved.");
        }

        var req = await requirementRepo.GetByIdAsync(proposal.RequirementId, ct)
            ?? throw new KeyNotFoundException($"Requirement '{proposal.RequirementId}' was not found.");

        var newRevisionNumber = req.CurrentRevisionNumber + 1;

        var revision = new RequirementRevision
        {
            Id = Guid.NewGuid(),
            RequirementId = req.Id,
            RevisionNumber = newRevisionNumber,
            Title = proposal.ProposedTitle,
            Body = proposal.ProposedBody,
            Status = RequirementStatus.Active,
            ChangedByUserId = approverUserId,
            ChangeReason = proposal.ChangeReason,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await revisionRepo.AddAsync(revision, ct);

        var updatedReq = req with
        {
            Title = proposal.ProposedTitle,
            Body = proposal.ProposedBody,
            CurrentRevisionNumber = newRevisionNumber,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await requirementRepo.UpdateAsync(updatedReq, ct);
        return revision;
    }
}
