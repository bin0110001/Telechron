namespace Telechron.Host.Workflows;

using System.Text.Json;
using Telechron.Host.Modules.Runtime;
using Telechron.Sdk.Containers;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules.Functions;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Workflows;
using Telechron.Sdk.Workflows.Approvals;

public sealed class WorkflowEngine(
    IWorkflowRepository workflowRepo,
    IWorkflowRunRepository runRepo,
    IArtifactRepository artifactRepo,
    IApprovalManager approvalManager,
    IModuleRuntime moduleRuntime,
    IContainerExecutionService? containerExecutionService = null) : IWorkflowEngine
{
    public async Task<WorkflowRun> StartWorkflowAsync(
        Guid workflowId, Dictionary<string, string>? inputVariables = null, CancellationToken ct = default)
    {
        var workflow = await workflowRepo.GetByIdAsync(workflowId, ct)
            ?? throw new KeyNotFoundException($"Workflow '{workflowId}' was not found.");

        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(workflow.DefinitionJson)
            ?? throw new InvalidOperationException($"Invalid WorkflowDefinition JSON in workflow '{workflowId}'.");

        var mergedVariables = new Dictionary<string, string>(definition.Variables);
        if (inputVariables != null)
        {
            foreach (var (k, v) in inputVariables)
            {
                mergedVariables[k] = v;
            }
        }

        var pinnedDefinition = definition with { Variables = mergedVariables };
        var snapshotJson = JsonSerializer.Serialize(pinnedDefinition);

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            Status = WorkflowRunStatus.Pending,
            DefinitionSnapshotJson = snapshotJson,
            StartedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = null
        };

        await runRepo.AddAsync(run, ct);
        return await ExecuteRunAsync(run.Id, ct);
    }

    public async Task<WorkflowRun> ExecuteRunAsync(Guid workflowRunId, CancellationToken ct = default)
    {
        var run = await runRepo.GetByIdAsync(workflowRunId, ct)
            ?? throw new KeyNotFoundException($"WorkflowRun '{workflowRunId}' was not found.");

        if (run.Status is WorkflowRunStatus.Passed or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled)
        {
            return run;
        }

        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(run.DefinitionSnapshotJson)
            ?? throw new InvalidOperationException($"Invalid DefinitionSnapshotJson in WorkflowRun '{workflowRunId}'.");

        var updatedRun = run with { Status = WorkflowRunStatus.Running };
        await runRepo.UpdateAsync(updatedRun, ct);

        var executedStepIds = new HashSet<string>();
        var failedStepIds = new HashSet<string>();
        var stepOutputs = new Dictionary<string, string>();

        var pendingSteps = new List<WorkflowStepDefinition>(definition.Steps);

        while (pendingSteps.Count > 0)
        {
            var readySteps = pendingSteps
                .Where(s => s.DependsOnStepIds.All(dep => executedStepIds.Contains(dep)))
                .ToList();

            if (readySteps.Count == 0)
            {
                // Cannot make forward progress (unresolvable cycle or dependencies failed)
                if (failedStepIds.Count > 0 && definition.FailurePolicy == WorkflowFailurePolicy.ContinueOnError)
                {
                    var partialRun = updatedRun with
                    {
                        Status = WorkflowRunStatus.PartiallyFailed,
                        CompletedAtUtc = DateTimeOffset.UtcNow
                    };
                    await runRepo.UpdateAsync(partialRun, ct);
                    return partialRun;
                }

                var failedRun = updatedRun with
                {
                    Status = WorkflowRunStatus.Failed,
                    CompletedAtUtc = DateTimeOffset.UtcNow
                };
                await runRepo.UpdateAsync(failedRun, ct);
                return failedRun;
            }

            foreach (var step in readySteps)
            {
                pendingSteps.Remove(step);

                // Check Approval Gate
                if (step.ApprovalGate is not null)
                {
                    var runRequests = await approvalManager.GetRequestsForRunAsync(workflowRunId, ct);
                    var existingRequest = runRequests.FirstOrDefault(r => r.StepId == step.Id && r.GateId == step.ApprovalGate.GateId);

                    if (existingRequest is null || !existingRequest.IsSatisfied)
                    {
                        if (existingRequest is null)
                        {
                            await approvalManager.CreateRequestAsync(
                                workflowRunId, step.Id, step.ApprovalGate.GateId, step.ApprovalGate.Prompt, ct);
                        }

                        var awaitingRun = updatedRun with { Status = WorkflowRunStatus.AwaitingApproval };
                        await runRepo.UpdateAsync(awaitingRun, ct);
                        return awaitingRun;
                    }
                }

                // Resolve parameters
                var resolvedParams = ResolveParameters(step.Parameters, definition.Variables, stepOutputs);

                // Execute Step
                var stepResult = await ExecuteStepAsync(step, resolvedParams, ct);

                if (stepResult.Succeeded)
                {
                    executedStepIds.Add(step.Id);
                    stepOutputs[step.Id] = stepResult.OutputSummary;

                    // Record artifact if produced
                    if (step.OutputArtifactTypes.Count > 0)
                    {
                        var artifact = new Artifact
                        {
                            Id = Guid.NewGuid(),
                            WorkflowRunId = workflowRunId,
                            ArtifactType = step.OutputArtifactTypes.First(),
                            Name = $"{step.Id}-output",
                            BlobRef = $"artifacts/{workflowRunId}/{step.Id}.dat",
                            SizeBytes = 0,
                            CreatedAtUtc = DateTimeOffset.UtcNow
                        };
                        await artifactRepo.AddAsync(artifact, ct);
                    }
                }
                else
                {
                    failedStepIds.Add(step.Id);
                    if (definition.FailurePolicy == WorkflowFailurePolicy.FailFast)
                    {
                        var failedRun = updatedRun with
                        {
                            Status = WorkflowRunStatus.Failed,
                            CompletedAtUtc = DateTimeOffset.UtcNow
                        };
                        await runRepo.UpdateAsync(failedRun, ct);
                        return failedRun;
                    }
                }
            }
        }

        var finalStatus = failedStepIds.Count > 0
            ? WorkflowRunStatus.PartiallyFailed
            : WorkflowRunStatus.Passed;

        var completedRun = updatedRun with
        {
            Status = finalStatus,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };

        await runRepo.UpdateAsync(completedRun, ct);
        return completedRun;
    }

    public async Task<WorkflowRun> ResumeRunAsync(Guid workflowRunId, Guid approvalRequestId, CancellationToken ct = default)
    {
        var request = await approvalManager.GetRequestByIdAsync(approvalRequestId, ct)
            ?? throw new KeyNotFoundException($"Approval request '{approvalRequestId}' was not found.");

        if (!request.IsSatisfied)
        {
            throw new InvalidOperationException($"Approval request '{approvalRequestId}' is not yet satisfied.");
        }

        return await ExecuteRunAsync(workflowRunId, ct);
    }

    public async Task<WorkflowRun> CancelRunAsync(Guid workflowRunId, string reason, CancellationToken ct = default)
    {
        var run = await runRepo.GetByIdAsync(workflowRunId, ct)
            ?? throw new KeyNotFoundException($"WorkflowRun '{workflowRunId}' was not found.");

        var cancelledRun = run with
        {
            Status = WorkflowRunStatus.Cancelled,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };

        await runRepo.UpdateAsync(cancelledRun, ct);
        return cancelledRun;
    }

    private static string ResolveParameters(
        IReadOnlyDictionary<string, string> stepParameters,
        IReadOnlyDictionary<string, string> variables,
        IReadOnlyDictionary<string, string> stepOutputs)
    {
        var resolved = new Dictionary<string, string>();
        foreach (var (k, v) in stepParameters)
        {
            var value = v;
            foreach (var (varKey, varVal) in variables)
            {
                value = value.Replace($"${{{varKey}}}", varVal);
            }
            foreach (var (stepId, outputVal) in stepOutputs)
            {
                value = value.Replace($"${{steps.{stepId}.output}}", outputVal);
            }
            resolved[k] = value;
        }
        return JsonSerializer.Serialize(resolved);
    }

    private async Task<FunctionInvocationResult> ExecuteStepAsync(
        WorkflowStepDefinition step, string parametersJson, CancellationToken ct)
    {
        var moduleName = step.ModuleId ?? "telechron.functions.core";
        var executor = moduleRuntime.GetLoadedAs<IFunctionExecutorModule>(moduleName);

        if (executor is null)
        {
            return FunctionInvocationResult.Failure($"Module '{moduleName}' providing function kind '{step.FunctionKind}' is not loaded.");
        }

        if (executor.RequiresContainer(step.FunctionKind))
        {
            if (containerExecutionService is null)
            {
                return FunctionInvocationResult.Failure($"Function kind '{step.FunctionKind}' requires a container, but container execution service is not available.");
            }

            var argv = executor.BuildContainerCommand(step.FunctionKind, JsonSerializer.Serialize(step.InputArtifactTypes), parametersJson);
            var req = new ContainerExecutionRequest(
                "sha256:0000000000000000000000000000000000000000000000000000000000000000",
                argv,
                "/workspace",
                new ContainerResourceLimits(512 * 1024 * 1024, 1.0, 1024 * 1024 * 1024),
                NetworkPolicy.None,
                false,
                TimeSpan.FromMinutes(2));

            var res = await containerExecutionService.ExecuteAsync(req, ct);
            return res.ExitCode == 0
                ? FunctionInvocationResult.Success(JsonSerializer.Serialize(step.OutputArtifactTypes), res.StdOut)
                : FunctionInvocationResult.Failure($"Container execution failed (exit code {res.ExitCode}): {res.StdErr}");
        }

        return await executor.InvokeInProcessAsync(
            step.FunctionKind, JsonSerializer.Serialize(step.InputArtifactTypes), parametersJson, ct);
    }
}
