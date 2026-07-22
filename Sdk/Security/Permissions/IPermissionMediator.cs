namespace Telechron.Sdk.Security.Permissions;

public sealed record MediationResult(bool IsAuthorized, string Reason);

// R-MOD8a: the single non-bypassable Host-side authorization check every
// Persona tool/connector/workflow/secret access and every module capability
// use must pass through at dispatch time. Deny-by-default: a request whose
// Kind/ResourceId is not present in the supplied allowlist is denied,
// regardless of what the requesting Persona/LLM "believes" it's allowed to
// do — this is never satisfied by LLM self-restraint, only by this check.
// Every decision is audited (R-SEC7).
public interface IPermissionMediator
{
    Task<MediationResult> AuthorizeAsync(
        CapabilityRequest request,
        IReadOnlyCollection<CapabilityGrant> allowlist,
        CancellationToken ct = default);
}
