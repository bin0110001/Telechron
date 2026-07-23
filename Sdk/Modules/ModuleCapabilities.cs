using System.Text.Json;
using Telechron.Sdk.Security.Permissions;

namespace Telechron.Sdk.Modules;

// R-MOD8: Module.CapabilitiesJson's wire format -- a flat array of
// capability kind names (matching CapabilityKind's enum member names,
// e.g. ["FilesystemRead", "InternetAccess"]), each optionally scoped to a
// specific resource. Parsing lives here (Sdk) rather than duplicated
// between Host approval UI and dispatch-time mediation.
public static class ModuleCapabilities
{
    public static IReadOnlyList<CapabilityGrant> Parse(string capabilitiesJson)
    {
        if (string.IsNullOrWhiteSpace(capabilitiesJson))
            return [];

        var entries = JsonSerializer.Deserialize<List<CapabilityEntry>>(capabilitiesJson) ?? [];
        return entries
            .Where(e => Enum.TryParse<CapabilityKind>(e.Kind, ignoreCase: true, out _))
            .Select(e => new CapabilityGrant(Enum.Parse<CapabilityKind>(e.Kind, ignoreCase: true), e.ResourceId))
            .ToList();
    }

    public static string Serialize(IReadOnlyList<CapabilityGrant> grants) =>
        JsonSerializer.Serialize(grants.Select(g => new CapabilityEntry(g.Kind.ToString(), g.ResourceId)));

    private sealed record CapabilityEntry(string Kind, string? ResourceId);
}
