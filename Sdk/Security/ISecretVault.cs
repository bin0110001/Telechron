namespace Telechron.Sdk.Security;

// R-SEC1/R-SEC5: the only path through which a raw secret value is ever
// resolved. Personas/prompts and tool-call construction see only opaque
// Secret.Handle strings (R-SEC1) — this interface is called strictly inside
// the Host/Connector runtime at the final hop before an outbound call
// (R-SEC5), never earlier. Revoked secrets fail resolution immediately
// (R-SEC8) rather than returning a stale value.
public interface ISecretVault
{
    // Creates a new Secret for a Project, encrypting the raw value at rest
    // and returning the opaque handle Personas/prompts will reference it by.
    Task<string> StoreAsync(Guid projectId, string name, ReadOnlyMemory<byte> rawValue, CancellationToken ct = default);

    // Resolves a handle to its raw value. Throws SecretRevokedException if the
    // secret has been revoked (R-SEC8) rather than silently succeeding with a
    // stale value or silently failing.
    Task<byte[]> ResolveAsync(string handle, CancellationToken ct = default);

    Task RevokeAsync(string handle, CancellationToken ct = default);

    // Rotation: replaces the raw value behind an existing handle, re-encrypting
    // under the current master key. The handle itself does not change, so
    // existing references (Connectors, Personas, Functions) keep working
    // without edits — only the value they resolve to changes (R-SEC8).
    Task RotateAsync(string handle, ReadOnlyMemory<byte> newRawValue, CancellationToken ct = default);
}

public sealed class SecretRevokedException(string handle)
    : Exception($"Secret handle '{handle}' has been revoked and can no longer be resolved.")
{
    public string Handle { get; } = handle;
}

public sealed class SecretNotFoundException(string handle)
    : Exception($"No secret found for handle '{handle}'.")
{
    public string Handle { get; } = handle;
}
