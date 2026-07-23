namespace Telechron.Sdk.Modules.Connectors;

// R-MOD9: "Modules may provide Connector implementations. Connector
// providers declare: authentication mechanisms, required secrets,
// supported artifact types, supported operations." R-SEC5: the raw
// secret is resolved and handed to ExecuteOperationAsync's callback only
// inside the caller's ISecretResolutionScope.ExecuteAsync -- this
// interface never receives or returns a raw secret value itself, only
// the opaque handle string a Connector row's SecretHandle already is.
public interface IConnectorModule : IModule
{
    string AuthenticationMechanism { get; }
    IReadOnlyList<string> SupportedArtifactTypes { get; }
    IReadOnlyList<string> SupportedOperations { get; }

    // secretBytes is only valid for the duration of this call -- callers
    // resolve it via ISecretResolutionScope.ExecuteAsync and this method
    // is what runs INSIDE that scope's finalHopCall callback. A Connector
    // module must never persist, log, or otherwise retain secretBytes
    // beyond this call.
    Task<ConnectorOperationResult> ExecuteOperationAsync(
        string operation, string parametersJson, ReadOnlyMemory<byte> secretBytes, CancellationToken ct = default);
}
