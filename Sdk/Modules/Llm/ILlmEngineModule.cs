namespace Telechron.Sdk.Modules.Llm;

// R-LLM1/R-LLM2: "LLM providers are resolved through a registry;
// connection configuration owns provider settings." An engine module IS
// a provider (Ollama, OpenAI, Claude, ...) -- the registry (Host-side,
// R-LLM1) maps LlmConnection.Provider strings to a loaded engine module
// instance; ConfigurationJson/SecretHandle on the LlmConnection row own
// per-connection settings (base URL, API key), never hardcoded here.
public interface ILlmEngineModule : IModule
{
    string ProviderName { get; }

    // secretBytes follows the same R-SEC5 shape as IConnectorModule --
    // empty for a connection with no SecretHandle (e.g. local Ollama).
    Task<LlmCompletionResult> CompleteAsync(
        LlmCompletionRequest request, string connectionConfigurationJson, ReadOnlyMemory<byte> secretBytes, CancellationToken ct = default);
}
