namespace Telechron.Sdk.Security.Permissions;

// One entry in a Persona/module's allowlist. ResourceId null = the grant
// covers every resource of Kind (e.g. "LlmAccess" broadly); a specific value
// scopes it (e.g. a single secret handle or connector ID).
public sealed record CapabilityGrant(CapabilityKind Kind, string? ResourceId);
