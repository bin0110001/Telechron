namespace Telechron.Host.Intent;

using System.Text.Json;
using Telechron.Host.Modules.Runtime;
using Telechron.Sdk.Intent;
using Telechron.Sdk.Modules.Functions;
using Telechron.Sdk.Workflows;

public sealed class CapabilityGapAnalyzer(IModuleRuntime moduleRuntime) : ICapabilityGapAnalyzer
{
    public Task<CapabilityGapReport> AnalyzeGapsAsync(string workflowDefinitionJson, CancellationToken ct = default)
    {
        WorkflowDefinition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<WorkflowDefinition>(workflowDefinitionJson);
        }
        catch
        {
            return Task.FromResult(new CapabilityGapReport
            {
                HasGaps = true,
                MissingFunctionKinds = ["invalid-definition"],
                MissingModuleTypes = ["invalid-definition"],
                DetailsJson = """{"error": "Failed to parse workflow definition JSON"}"""
            });
        }

        if (definition is null || definition.Steps.Count == 0)
        {
            return Task.FromResult(new CapabilityGapReport { HasGaps = false });
        }

        var missingKinds = new HashSet<string>();

        foreach (var step in definition.Steps)
        {
            var moduleName = step.ModuleId ?? "telechron.functions.core";
            var executor = moduleRuntime.GetLoadedAs<IFunctionExecutorModule>(moduleName);

            if (executor is null || !executor.SupportedFunctionKinds.Contains(step.FunctionKind))
            {
                missingKinds.Add(step.FunctionKind);
            }
        }

        var hasGaps = missingKinds.Count > 0;
        return Task.FromResult(new CapabilityGapReport
        {
            HasGaps = hasGaps,
            MissingFunctionKinds = missingKinds.ToList(),
            MissingModuleTypes = missingKinds.Select(k => $"function-executor:{k}").ToList(),
            DetailsJson = JsonSerializer.Serialize(new { missingFunctionKinds = missingKinds.ToList() })
        });
    }
}
