using System.Text.Json;
using Telechron.Sdk.Security.Audit;
using Telechron.Sdk.Security.Permissions;

namespace Telechron.Host.Security.Permissions;

// R-MOD8a: deny-by-default authorization. A request is authorized only if the
// supplied allowlist contains a CapabilityGrant matching both Kind and
// ResourceId (or a Kind-wide grant with ResourceId == null). This is the only
// path capability checks flow through — callers (Personas, module dispatch,
// Phase 5+) must invoke this rather than trusting their own allowlist copies,
// so there is exactly one enforcement point to audit and reason about.
public sealed class PermissionMediator(IAuditLog auditLog) : IPermissionMediator
{
    public async Task<MediationResult> AuthorizeAsync(
        CapabilityRequest request, IReadOnlyCollection<CapabilityGrant> allowlist, CancellationToken ct = default)
    {
        var granted = allowlist.Any(g =>
            g.Kind == request.Kind &&
            (g.ResourceId is null || string.Equals(g.ResourceId, request.ResourceId, StringComparison.Ordinal)));

        var result = granted
            ? new MediationResult(true, "Matched an allowlist grant.")
            : new MediationResult(false, "No matching grant for this capability/resource in the supplied allowlist.");

        await auditLog.AppendAsync(
            granted ? AuditEventKind.CapabilityGranted : AuditEventKind.AuthorizationDenied,
            JsonSerializer.Serialize(new
            {
                requestorId = request.RequestorId,
                kind = request.Kind.ToString(),
                resourceId = request.ResourceId,
                granted,
            }),
            projectId: request.ProjectId,
            ct: ct);

        return result;
    }
}
