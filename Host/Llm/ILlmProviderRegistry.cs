using Telechron.Sdk.Modules.Llm;

namespace Telechron.Host.Llm;

// R-LLM1: "LLM providers are resolved through a registry." Maps a
// provider name (LlmConnection.Provider, e.g. "ollama") to the loaded
// engine module instance -- callers never look up a module by its
// module-assembly Name, only by the provider name a Project's
// LlmConnection configuration declares.
public interface ILlmProviderRegistry
{
    ILlmEngineModule? Resolve(string providerName);
}
