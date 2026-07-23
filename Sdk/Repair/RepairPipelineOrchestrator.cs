using System.Text.Json;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Sdk.Repair;

// R-NS2/R-FIX2: THE single repair pipeline. Every repair-triggering path
// (a Run's Findings, a batch scan's Repair Plan -- R-FIX2a's "batch is the
// aggregate case, not a separate pipeline") enters here. Sequence:
// Concurrency lock (R-FIX9) -> Governance check (R-FIX3) -> Snapshot ->
// Generate Fix (deterministic-first, R-FIX5; LLM path carries Design
// Document context, R-DM6a) -> synthesis-needed check (R-FIX10/R-NS3) ->
// Apply (atomic, R-FIX7) -> privileged-path/diff-scope/oscillation gates
// (R-SEC4/R-FIX12/R-FIX11) -> Verify in container -> drift check
// (R-FIX13) -> Approval Gate (Project policy, forced by any gate above) ->
// Revert on failure or Commit + signed provenance (R-SEC3) on success.
public sealed class RepairPipelineOrchestrator(
    IRepairVersionControl versionControl,
    IRepairAttemptGovernor governor,
    IRepairConcurrencyGate concurrencyGate,
    IDeterministicFixProvider deterministicFixProvider,
    ILlmFixGenerator llmFixGenerator,
    IRepairVerifier verifier,
    IPrivilegedPathGuard privilegedPathGuard,
    IRepairDiffScopeGuard diffScopeGuard,
    IOscillationDetector oscillationDetector,
    IArchitecturalDriftDetector driftDetector,
    IRepairProvenanceSigner provenanceSigner,
    IRepairAttemptRepository repairAttemptRepository)
{
    public async Task<RepairOutcome> RunAsync(RepairRequest request, CancellationToken ct = default)
    {
        await using var lease = await concurrencyGate.AcquireAsync(request.ProjectRootPath, ct);

        var governance = await governor.CheckAsync(request.ProjectId, request.Findings, ct);
        if (governance.Declined)
            return new RepairOutcome(RepairOutcomeStatus.GovernanceDeclined, null, governance.Reason!, null, []);

        var snapshot = await versionControl.SnapshotAsync(request.ProjectRootPath, ct);

        var fix = await GenerateFixAsync(request, ct);
        if (fix is null)
            return new RepairOutcome(RepairOutcomeStatus.NoFixProduced, null, "No deterministic or LLM fix was produced.", null, []);

        if (fix.RequiresSynthesis)
        {
            // R-FIX10/R-NS3: a fix needing a NEW capability never
            // auto-applies, regardless of Project policy -- it routes
            // straight to the human/R-BUILD5 gate without ever touching
            // the working tree.
            var synthesisAttemptId = await PersistAttemptAsync(request, snapshot, patch: null, verifyResult: null, ct);
            return new RepairOutcome(RepairOutcomeStatus.PendingApproval, synthesisAttemptId,
                "Fix requires Capability Synthesis (new module/Function) -- routed to the R-BUILD5 human gate, never auto-applied.",
                null, ["Requires Capability Synthesis (R-FIX10/R-NS3)"]);
        }

        var patch = fix.Patch!;
        var forcedApprovalReasons = new List<string>();

        var privilegedCheck = privilegedPathGuard.Check(patch);
        if (privilegedCheck.IsPrivileged)
            forcedApprovalReasons.Add($"Privileged-path patch (R-SEC4): {string.Join(", ", privilegedCheck.MatchedPaths)}");

        var declaredOriginPaths = request.Findings
            .Select(f => f.OriginFilePath)
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
        var scopeCheck = diffScopeGuard.Check(patch, declaredOriginPaths);
        if (scopeCheck.ExceedsScope)
            forcedApprovalReasons.Add($"Diff scope limit (R-FIX12): {scopeCheck.Reason}");

        var priorSignatures = await GetPriorPatchSignaturesAsync(request.Findings, ct);
        var oscillationCheck = oscillationDetector.Check(patch, priorSignatures);
        if (oscillationCheck.IsOscillation)
            forcedApprovalReasons.Add($"Oscillation detected (R-FIX11): {oscillationCheck.Reason}");

        var applyResult = await versionControl.ApplyAsync(request.ProjectRootPath, patch, ct);
        if (!applyResult.Succeeded)
        {
            await versionControl.RevertToSnapshotAsync(request.ProjectRootPath, snapshot, ct);
            var failedApplyAttemptId = await PersistAttemptAsync(request, snapshot, patch, verifyResult: null, ct);
            return new RepairOutcome(RepairOutcomeStatus.Reverted, failedApplyAttemptId,
                $"Apply failed: {applyResult.ErrorMessage}", patch, forcedApprovalReasons);
        }

        var verifyResult = await verifier.VerifyAsync(request.ProjectRootPath, ct);
        if (!verifyResult.Succeeded)
        {
            await versionControl.RevertToSnapshotAsync(request.ProjectRootPath, snapshot, ct);
            var failedVerifyAttemptId = await PersistAttemptAsync(request, snapshot, patch, verifyResult, ct);
            return new RepairOutcome(RepairOutcomeStatus.Reverted, failedVerifyAttemptId,
                $"Verify failed: {verifyResult.RawOutput}", patch, forcedApprovalReasons);
        }

        var driftCheck = await driftDetector.CheckAsync(patch, request.ActiveRequirements, ct);
        if (driftCheck.IsDrift)
            forcedApprovalReasons.Add($"Architectural drift (R-FIX13): {driftCheck.Reason}");

        var attemptId = await PersistAttemptAsync(request, snapshot, patch, verifyResult, ct);

        var requiresApproval = request.ProjectPolicy == RepairPolicy.RequireApproval || forcedApprovalReasons.Count > 0;
        if (requiresApproval)
        {
            // Verified but paused -- the working tree is left AS-APPLIED
            // (not reverted) so a human reviewer can inspect the real
            // result; RevertToSnapshotAsync remains available if the
            // approval is later rejected (that decision path is a
            // separate, explicit operation, not built by this method).
            return new RepairOutcome(RepairOutcomeStatus.PendingApproval, attemptId,
                requiresApproval && forcedApprovalReasons.Count == 0
                    ? "Verify succeeded; Project Repair Policy is RequireApproval."
                    : "Verify succeeded but one or more gates forced human approval regardless of policy.",
                patch, forcedApprovalReasons);
        }

        var commit = await versionControl.CommitAsync(
            request.ProjectRootPath,
            BuildCommitMessage(request, patch),
            "Telechron Repair Pipeline",
            "repair@telechron.internal",
            ct);

        await AttachProvenanceAsync(attemptId, request, commit, verifyResult, ct);

        return new RepairOutcome(RepairOutcomeStatus.Committed, attemptId,
            $"Verified and committed as {commit.CommitReference} (FullyAutonomous policy, no forcing gate triggered).",
            patch, []);
    }

    private async Task<FixCandidate?> GenerateFixAsync(RepairRequest request, CancellationToken ct)
    {
        // R-FIX5: deterministic fixes execute before LLM-based fixes.
        // Only single-Finding deterministic fixes are attempted -- a
        // multi-Finding Repair Plan (R-FIX2a) always goes through the LLM
        // path, since reconciling several deterministic single-Finding
        // patches into one atomic multi-file transaction is exactly the
        // kind of bespoke merge logic R-ENG4 rules out; the LLM path
        // already natively handles N Findings at once.
        if (request.Findings.Count == 1)
        {
            var deterministic = await deterministicFixProvider.TryFixAsync(request.Findings[0], request.ProjectRootPath, ct);
            if (deterministic.Handled)
                return new FixCandidate(deterministic.Patch, RequiresSynthesis: false);
        }

        var relevantFiles = ReadRelevantFiles(request);
        var llmContext = new LlmFixContext(request.Findings, request.ActiveRequirements, request.DesignDocument, relevantFiles);
        var llmResult = await llmFixGenerator.GenerateAsync(llmContext, ct);

        if (!llmResult.Succeeded)
            return null;

        return llmResult.RequiresCapabilitySynthesis
            ? new FixCandidate(null, RequiresSynthesis: true)
            : new FixCandidate(llmResult.Patch, RequiresSynthesis: false);
    }

    private static IReadOnlyDictionary<string, string> ReadRelevantFiles(RepairRequest request)
    {
        var files = new Dictionary<string, string>();
        foreach (var path in request.Findings.Select(f => f.OriginFilePath).Where(p => p is not null).Distinct())
        {
            var fullPath = Path.Combine(request.ProjectRootPath, path!);
            if (File.Exists(fullPath))
                files[path!] = File.ReadAllText(fullPath);
        }

        return files;
    }

    private async Task<IReadOnlyList<string>> GetPriorPatchSignaturesAsync(IReadOnlyList<Finding> findings, CancellationToken ct)
    {
        var signatures = new List<string>();
        foreach (var finding in findings)
        {
            var priorAttempts = await repairAttemptRepository.GetByFindingAsync(finding.Id, ct);
            signatures.AddRange(priorAttempts
                .Where(a => a.ApprovalDecision != RepairApprovalDecision.Approved)
                .Select(a => oscillationDetector.ComputeSignature(new PatchDiff(
                    [new PatchFileChange("__stored__", a.PatchDiff)]))));
        }

        return signatures;
    }

    private async Task<Guid> PersistAttemptAsync(
        RepairRequest request, SnapshotRef snapshot, PatchDiff? patch, VerifyResult? verifyResult, CancellationToken ct)
    {
        var attempt = new RepairAttempt
        {
            Id = Guid.NewGuid(),
            FindingIds = request.Findings.Select(f => f.Id).ToList(),
            SnapshotRef = snapshot.Value,
            PatchDiff = patch is null ? string.Empty : SerializePatch(patch),
            VerifyResultJson = verifyResult is null ? null : JsonSerializer.Serialize(verifyResult.TestRunResult),
            ApprovalDecision = null,
            ApproverUserId = null,
            ResultingArtifactId = null,
            CommitReference = null,
            ProvenanceRecordJson = null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        await repairAttemptRepository.AddAsync(attempt, ct);
        return attempt.Id;
    }

    private async Task AttachProvenanceAsync(
        Guid attemptId, RepairRequest request, CommitResult commit, VerifyResult verifyResult, CancellationToken ct)
    {
        var record = new RepairProvenanceRecord(
            RepairAttemptId: attemptId,
            FindingIds: request.Findings.Select(f => f.Id).ToList(),
            CommitReference: commit.CommitReference,
            GeneratingPersonaId: null,
            LlmConnectionId: null,
            LlmModelUsed: null,
            VerifySucceeded: verifyResult.Succeeded,
            VerifySummary: verifyResult.RawOutput,
            SignedAtUtc: DateTimeOffset.UtcNow);

        var signed = provenanceSigner.Sign(record);

        var existing = await repairAttemptRepository.GetByIdAsync(attemptId, ct)
            ?? throw new InvalidOperationException($"RepairAttempt {attemptId} not found after PersistAttemptAsync.");

        var updated = existing with
        {
            CommitReference = commit.CommitReference,
            ProvenanceRecordJson = JsonSerializer.Serialize(signed),
        };

        await repairAttemptRepository.UpdateAsync(updated, ct);
    }

    private static string SerializePatch(PatchDiff patch) => JsonSerializer.Serialize(patch);

    private static string BuildCommitMessage(RepairRequest request, PatchDiff patch) =>
        $"Telechron auto-repair: {request.Findings.Count} Finding(s), {patch.FileChanges.Count} file(s) changed.";

    private sealed record FixCandidate(PatchDiff? Patch, bool RequiresSynthesis);
}
