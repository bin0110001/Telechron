namespace Telechron.Host.Reliability;

using Telechron.Host.DesignDocs;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Reliability;
using Telechron.Sdk.Repair;
using Telechron.Sdk.Workflows.Approvals;

public sealed class HostSentinel(
    ReflexiveDesignDocWire reflexiveWire,
    RepairPipelineOrchestrator? repairOrchestrator,
    IApprovalManager approvalManager) : IHostSentinel
{
    public async Task<HostSelfRepairReport> RunSelfRepairCheckAsync(CancellationToken ct = default)
    {
        var selfDesignDoc = await reflexiveWire.GetTelechronSelfDesignDocumentAsync(ct);

        // Self-repair of Host skeleton is a privileged path (R-SEC4) -> ALWAYS RequireApproval
        var prompt = "Host Sentinel detected self-repair candidate in Telechron skeleton. Approve self-repair execution?";
        var approvalRequest = await approvalManager.CreateRequestAsync(
            ReflexiveDesignDocWire.TelechronSelfProjectId, "sentinel-self-repair", "R-SEC4-privileged-path", prompt, ct);

        var finding = new Finding
        {
            Id = Guid.NewGuid(),
            ProjectId = ReflexiveDesignDocWire.TelechronSelfProjectId,
            RunId = Guid.NewGuid(),
            WorkflowRunId = null,
            OriginFilePath = "Host/Skeleton.cs",
            RootCauseSignature = "sig-host-skeleton-selfrepair",
            Severity = FindingSeverity.Error,
            Category = "HostSelfRepair",
            FailureClass = FindingFailureClass.Code,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var repairAttempt = new RepairAttempt
        {
            Id = Guid.NewGuid(),
            FindingIds = [finding.Id],
            SnapshotRef = "HEAD",
            PatchDiff = "// Host Sentinel repair patch",
            ApprovalDecision = null,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _ = repairOrchestrator; // Seam for orchestrator call
        _ = selfDesignDoc;

        return new HostSelfRepairReport
        {
            RepairNeeded = true,
            RequiresHumanApproval = true,
            RepairAttempt = repairAttempt,
            Reason = "Host Sentinel self-repair is a privileged path and requires explicit human approval."
        };
    }
}
