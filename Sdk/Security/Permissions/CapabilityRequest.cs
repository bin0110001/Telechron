namespace Telechron.Sdk.Security.Permissions;

// A single dispatch-time ask: "may {RequestorId} use {Kind} against
// {ResourceId}?" ResourceId is the specific target within the capability kind
// — a secret handle for SecretAccess, a connector ID for ConnectorAccess, a
// tool name for ToolInvocation, etc. Null means "any" (used when the
// allowlist itself is kind-scoped rather than resource-scoped).
public sealed record CapabilityRequest(
    Guid RequestorId,
    CapabilityKind Kind,
    string? ResourceId,
    Guid? ProjectId);
