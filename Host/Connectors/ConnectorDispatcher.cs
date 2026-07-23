using Microsoft.Extensions.Logging;
using Telechron.Host.Modules.Permissions;
using Telechron.Host.Modules.Runtime;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules.Connectors;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Security;
using Telechron.Sdk.Security.Permissions;

namespace Telechron.Host.Connectors;

public sealed class ConnectorDispatcher(
    IModuleRuntime moduleRuntime,
    IModuleRepository moduleRepository,
    IModuleCapabilityMediator capabilityMediator,
    ISecretResolutionScope secretResolutionScope,
    ILogger<ConnectorDispatcher> logger) : IConnectorDispatcher
{
    public async Task<ConnectorOperationResult> DispatchAsync(
        Connector connector, Guid projectId, string operation, string parametersJson, CancellationToken ct = default)
    {
        var module = await moduleRepository.GetByIdAsync(connector.ModuleId, ct);
        if (module is null)
            return ConnectorOperationResult.Failure($"Module '{connector.ModuleId}' backing Connector '{connector.Name}' is not registered.");

        // R-MOD8a: dispatch-time, non-bypassable -- checked before the
        // loaded module instance is ever touched, using the module's own
        // Project-approved capability set, not what the Connector "claims"
        // to need.
        var authorization = await capabilityMediator.AuthorizeAsync(module, projectId, CapabilityKind.ConnectorAccess, connector.Id.ToString(), ct);
        if (!authorization.IsAuthorized)
        {
            logger.LogWarning("Connector dispatch denied for '{ConnectorName}': {Reason}", connector.Name, authorization.Reason);
            return ConnectorOperationResult.Failure($"Not authorized: {authorization.Reason}");
        }

        var connectorModule = moduleRuntime.GetLoadedAs<IConnectorModule>(module.Name);
        if (connectorModule is null)
            return ConnectorOperationResult.Failure($"Module '{module.Name}' is not currently loaded as an IConnectorModule.");

        if (connector.SecretHandle is null)
        {
            // Some Connectors legitimately need no secret (e.g. an
            // unauthenticated public API) -- pass an empty span rather
            // than forcing every Connector through the resolution scope.
            return await connectorModule.ExecuteOperationAsync(operation, parametersJson, ReadOnlyMemory<byte>.Empty, ct);
        }

        // R-SEC5: the raw secret exists only inside this callback -- it is
        // never assigned to a local outside ExecuteAsync, never returned
        // from DispatchAsync, never logged.
        return await secretResolutionScope.ExecuteAsync(
            connector.SecretHandle,
            secretBytes => connectorModule.ExecuteOperationAsync(operation, parametersJson, secretBytes, ct),
            ct);
    }
}
