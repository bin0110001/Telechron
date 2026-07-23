using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Security.Permissions;

namespace Telechron.Host.Modules.Permissions;

public sealed class ModuleCapabilityMediator(IPermissionMediator permissionMediator) : IModuleCapabilityMediator
{
    public Task<MediationResult> AuthorizeAsync(
        Module module, Guid projectId, CapabilityKind kind, string? resourceId = null, CancellationToken ct = default)
    {
        var allowlist = ModuleCapabilities.Parse(module.CapabilitiesJson);
        var request = new CapabilityRequest(module.Id, kind, resourceId, projectId);
        return permissionMediator.AuthorizeAsync(request, allowlist, ct);
    }
}
