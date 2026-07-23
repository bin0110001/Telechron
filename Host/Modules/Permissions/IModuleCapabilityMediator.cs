using Telechron.Sdk.Domain;
using Telechron.Sdk.Security.Permissions;

namespace Telechron.Host.Modules.Permissions;

// R-MOD8/R-MOD8a: "All... module capability use... [is] mediated by a
// non-bypassable Host-side authorization check at dispatch time." This is
// that check for modules specifically -- a thin adapter that builds a
// CapabilityRequest from a Module's own approved CapabilitiesJson (the
// allowlist a Project already approved, R-MOD8) and calls through the
// single shared IPermissionMediator (Host/Security/Permissions), never a
// parallel authorization path.
public interface IModuleCapabilityMediator
{
    Task<MediationResult> AuthorizeAsync(
        Module module, Guid projectId, CapabilityKind kind, string? resourceId = null, CancellationToken ct = default);
}
