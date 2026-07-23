namespace Telechron.Host.Llm;

// R-LLM1: the registry's own config -- which loaded module (by
// ModuleRuntime module name) backs each provider name. An operator adds
// an entry here (or a future admin UI writes it) when a new engine
// module is installed; the registry itself never guesses.
public sealed class LlmProviderRegistryOptions
{
    // providerName -> module name (e.g. "ollama" -> "telechron.llm.ollama").
    public Dictionary<string, string> ProviderToModuleName { get; set; } = [];
}
