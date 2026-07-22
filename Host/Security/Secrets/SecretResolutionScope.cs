using Telechron.Sdk.Security;

namespace Telechron.Host.Security.Secrets;

// R-SEC5: raw secrets are resolved and injected strictly inside this scope,
// at the final hop before an outbound Connector/Function call — never inside
// Persona tool-call construction. ScrubForPromptReentry is the safety net for
// the common case where a tool result echoes request content (including a
// resolved secret) back verbatim.
public sealed class SecretResolutionScope(ISecretVault secretVault, ISecretFingerprintRegistry fingerprints) : ISecretResolutionScope
{
    public async Task<TResult> ExecuteAsync<TResult>(
        string handle, Func<ReadOnlyMemory<byte>, Task<TResult>> finalHopCall, CancellationToken ct = default)
    {
        var rawValue = await secretVault.ResolveAsync(handle, ct);
        using var _ = fingerprints.Track(rawValue);
        try
        {
            return await finalHopCall(rawValue);
        }
        finally
        {
            Array.Clear(rawValue);
        }
    }

    public string ScrubForPromptReentry(string toolResult, IReadOnlyCollection<string> handlesInScope)
    {
        // Defense in depth: this does not re-resolve secrets (that would defeat
        // the purpose) — it is a structural placeholder for redaction logic that,
        // once Connectors exist (Phase 6), can compare tool output against
        // resolved-value fingerprints captured during ExecuteAsync. For now it
        // strips any literal handle references, since a handle leaking back into
        // a prompt is itself a smaller but related exposure (it reveals which
        // secret was used).
        var scrubbed = toolResult;
        foreach (var handle in handlesInScope)
        {
            scrubbed = scrubbed.Replace(handle, "[secret-handle-redacted]", StringComparison.Ordinal);
        }

        return scrubbed;
    }
}
