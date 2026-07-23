using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Telechron.Sdk.Containers;

namespace Telechron.Agent.Containers;

// R-SYS9: enforces "pinned by digest — never mutable tags — sourced from an
// allowlisted registry" for every image this Agent will pull/run. Runs
// before ContainerExecutionService ever calls into Podman.
public sealed partial class ImageProvenanceVerifier(IOptions<RegistryAllowlist> allowlist) : IImageProvenanceVerifier
{
    // registry[/path...]@sha256:<64 hex chars> — no mutable tag form accepted.
    [GeneratedRegex(@"^(?<registry>[a-zA-Z0-9.-]+(?::\d+)?)(?<path>/[a-zA-Z0-9._/-]+)?@sha256:[a-fA-F0-9]{64}$")]
    private static partial Regex DigestReferencePattern();

    public ImageProvenanceResult Verify(string imageReference)
    {
        var match = DigestReferencePattern().Match(imageReference);
        if (!match.Success)
        {
            return new ImageProvenanceResult(false,
                $"'{imageReference}' is not a digest-pinned reference (expected registry/path@sha256:<digest>). " +
                "Mutable tags are never accepted (R-SYS9).");
        }

        var registry = match.Groups["registry"].Value;
        var path = match.Groups["path"].Success ? match.Groups["path"].Value.TrimStart('/') : string.Empty;
        var registryAndMaybePath = allowlist.Value.AllowedRegistries.Any(allowed =>
        {
            // Support both a bare registry allowlist entry ("mcr.microsoft.com")
            // and a registry+path-prefix entry ("docker.io/library").
            if (!allowed.Contains('/'))
                return string.Equals(registry, allowed, StringComparison.OrdinalIgnoreCase);

            var allowedParts = allowed.Split('/', 2);
            return string.Equals(registry, allowedParts[0], StringComparison.OrdinalIgnoreCase)
                && path.StartsWith(allowedParts[1], StringComparison.OrdinalIgnoreCase);
        });

        return registryAndMaybePath
            ? new ImageProvenanceResult(true, "Digest-pinned reference from an allowlisted registry.")
            : new ImageProvenanceResult(false, $"Registry '{registry}' is not in the allowlist (R-SYS9).");
    }
}
