using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules.Connectors;

namespace Telechron.Host.Connectors;

// R-MOD9/R-SEC5/R-MOD8a: the one path a Connector operation is ever
// invoked through. Ties together capability mediation (is this Connector
// authorized for ConnectorAccess against this specific Connector
// resource, checked at dispatch time, never by caller self-restraint)
// and the secret-resolution boundary (the raw secret is resolved and
// handed to the module only inside ISecretResolutionScope's callback,
// never returned to or held by this dispatcher itself).
public interface IConnectorDispatcher
{
    Task<ConnectorOperationResult> DispatchAsync(
        Connector connector, Guid projectId, string operation, string parametersJson, CancellationToken ct = default);
}
