namespace Telechron.Host.Reliability;

using Telechron.Host.DesignDocs;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Reliability;
using Telechron.Sdk.Repair;

// R-REL3: the Host's own self-repair loop is a thin caller of the SAME
// RepairPipelineOrchestrator every managed Project's repair Persona uses
// (R-ENG4 -- no bespoke second pipeline). Self-repair of the repair engine
// itself is a privileged path (R-SEC4) -> the RepairRequest is always built
// with RepairPolicy.RequireApproval, so even a patch that would otherwise
// qualify for autonomous commit under a different policy cannot bypass
// human review here.
public sealed class HostSentinel(
    ReflexiveDesignDocWire reflexiveWire,
    IRepairPipelineFactory repairPipelineFactory,
    IFindingRepository findingRepository,
    IRequirementRepository requirementRepository,
    IRepairAttemptRepository repairAttemptRepository,
    IMachineRepository machineRepository) : IHostSentinel
{
    private const string ProjectRootPath = ".";

    public async Task<HostSelfRepairReport> RunSelfRepairCheckAsync(CancellationToken ct = default)
    {
        var selfDesignDoc = await reflexiveWire.GetTelechronSelfDesignDocumentAsync(ct);

        var openFindings = (await findingRepository.GetByProjectAsync(ReflexiveDesignDocWire.TelechronSelfProjectId, ct))
            .Where(f => f.FailureClass == FindingFailureClass.Code && f.FixStatus is null or "Open")
            .ToList();

        if (openFindings.Count == 0)
        {
            return new HostSelfRepairReport
            {
                RepairNeeded = false,
                RequiresHumanApproval = true,
                RepairAttempt = null,
                Reason = "No open Code Findings against Telechron's own reflexive Project."
            };
        }

        var activeRequirements = selfDesignDoc is not null
            ? (await requirementRepository.GetByDesignDocumentAsync(selfDesignDoc.Id, ct))
                .Where(r => r.Status == RequirementStatus.Active)
                .ToList()
            : [];

        var request = new RepairRequest(
            ReflexiveDesignDocWire.TelechronSelfProjectId,
            ProjectRootPath,
            // R-SEC4/R-REL3: never FullyAutonomous for the Host's own repair
            // engine, regardless of what any Project-level policy setting
            // might otherwise say -- this is not a Project, it has no
            // configurable policy of its own.
            RepairPolicy.RequireApproval,
            openFindings,
            activeRequirements,
            selfDesignDoc);

        // No Project-to-Machine assignment policy exists yet (same gap
        // Storefront's TargetMachineId parameter documents) -- self-repair
        // uses whichever registered Machine is available; a real deployment
        // with multiple Agents should pin this once such a policy exists.
        var machines = await machineRepository.GetAllAsync(ct);
        var machineId = machines.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("No registered Machine available to run the self-repair Verify stage.");

        var repairOrchestrator = await repairPipelineFactory.CreateForProjectAsync(ReflexiveDesignDocWire.TelechronSelfProjectId, machineId, ct);
        var outcome = await repairOrchestrator.RunAsync(request, ct);

        var repairAttempt = outcome.RepairAttemptId is { } attemptId
            ? await repairAttemptRepository.GetByIdAsync(attemptId, ct)
            : null;

        return new HostSelfRepairReport
        {
            RepairNeeded = true,
            RequiresHumanApproval = true,
            RepairAttempt = repairAttempt,
            Reason = outcome.Reason
        };
    }
}
