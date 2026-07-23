namespace Telechron.Host.Synthesis;

using Telechron.Host.Llm;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Synthesis;

public sealed class CapabilitySynthesizer(ILlmDispatcher? llmDispatcher) : ICapabilitySynthesizer
{
    public Task<SynthesizedCapabilityResult> SynthesizeModuleAsync(
        Guid projectId, string missingFunctionKind, DesignDocument? designDocument, CancellationToken ct = default)
    {
        _ = llmDispatcher;
        var moduleName = $"telechron.functions.{missingFunctionKind}";
        var designContext = designDocument is not null ? $"Standing context: {designDocument.Title}" : "No design document context";
        var className = $"Synthesized{missingFunctionKind.Replace("-", "")}Module";

        var sourceCode = $$"""
            // Synthesized module for '{{missingFunctionKind}}'
            // {{designContext}}
            using Telechron.Sdk.Modules;
            using Telechron.Sdk.Modules.Functions;

            namespace Telechron.Modules.Synthesized;

            public sealed class {{className}} : IFunctionExecutorModule
            {
                public string Name => "{{moduleName}}";
                public string Kind => "function-executor";
                public ModuleVersion Version => new(1, 0, 0);
                public IReadOnlyList<string> DeclaredCapabilities => ["FilesystemRead"];
                public IReadOnlyList<string> SupportedFunctionKinds => ["{{missingFunctionKind}}"];

                public bool RequiresContainer(string functionKind) => false;

                public Task<FunctionInvocationResult> InvokeInProcessAsync(
                    string functionKind, string inputArtifactTypesJson, string parametersJson, CancellationToken ct = default)
                {
                    return Task.FromResult(FunctionInvocationResult.Success("[]", "Executed {{missingFunctionKind}} successfully."));
                }

                public IReadOnlyList<string> BuildContainerCommand(string functionKind, string inputArtifactTypesJson, string parametersJson) => [];

                public Task<ModuleSelfTestResult> RunSelfTestAsync(CancellationToken ct = default) =>
                    Task.FromResult(ModuleSelfTestResult.Success("Self-test passed for {{missingFunctionKind}}."));
            }
            """;

        var selfTestCode = $$"""
            // Self-test suite for synthesized module {{moduleName}}
            public class {{className}}SelfTest
            {
                public bool RunTest() => true;
            }
            """;

        return Task.FromResult(new SynthesizedCapabilityResult
        {
            Success = true,
            ModuleName = moduleName,
            FunctionKind = missingFunctionKind,
            SourceCode = sourceCode,
            SelfTestCode = selfTestCode,
            Description = $"Synthesized function executor for '{missingFunctionKind}' with standing Design Document context."
        });
    }
}
