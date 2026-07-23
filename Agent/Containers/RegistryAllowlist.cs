namespace Telechron.Agent.Containers;

// R-SYS9: the registry allowlist. Mirrors containers/README.md's
// "Registry Allowlist" section, which remains the human-facing source of
// truth — keep the two in sync when either changes. Configurable so a
// deployment can extend it without a code change, but the default matches
// what's documented.
public sealed class RegistryAllowlist
{
    public IReadOnlyList<string> AllowedRegistries { get; set; } =
    [
        "mcr.microsoft.com",
        "docker.io/library",
    ];
}
