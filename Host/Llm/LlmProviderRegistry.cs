using Microsoft.Extensions.Options;
using Telechron.Host.Modules.Runtime;
using Telechron.Sdk.Modules.Llm;

namespace Telechron.Host.Llm;

public sealed class LlmProviderRegistry(IModuleRuntime moduleRuntime, IOptions<LlmProviderRegistryOptions> options) : ILlmProviderRegistry
{
    public ILlmEngineModule? Resolve(string providerName) =>
        options.Value.ProviderToModuleName.TryGetValue(providerName, out var moduleName)
            ? moduleRuntime.GetLoadedAs<ILlmEngineModule>(moduleName)
            : null;
}
