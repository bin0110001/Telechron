using System.Collections.Concurrent;
using System.Text;
using Telechron.Sdk.Security;

namespace Telechron.Host.Security.Logging;

// R-SEC1: tracks the plaintext of currently-in-scope secret values so the
// logging redaction safety net can catch accidental leakage. Values are only
// tracked for the lifetime of the returned IDisposable (matching
// ISecretResolutionScope.ExecuteAsync's scope) — nothing here persists beyond
// that, mirroring the short-lived-and-zeroed discipline SecretVault already
// applies to the raw bytes themselves.
public sealed class SecretFingerprintRegistry : ISecretFingerprintRegistry
{
    // Keyed by the plaintext itself so redaction is a direct substring check;
    // values only ever live here while genuinely in use, and the set is small
    // (bounded by concurrently in-flight secret resolutions).
    private readonly ConcurrentDictionary<string, byte> _activeValues = new(StringComparer.Ordinal);

    public IDisposable Track(ReadOnlySpan<byte> rawValue)
    {
        // Secrets are predominantly text (API keys, tokens, credentials) per
        // R-DM12's examples; skip tracking values that aren't valid UTF-8
        // (e.g. binary key material) since substring redaction doesn't apply.
        string? text;
        try
        {
            text = Encoding.UTF8.GetString(rawValue);
        }
        catch (DecoderFallbackException)
        {
            text = null;
        }

        if (string.IsNullOrEmpty(text) || text.Length < 6)
            return NullScope.Instance; // too short to fingerprint safely without false-positive risk

        _activeValues.TryAdd(text, 0);
        return new Untrack(this, text);
    }

    public bool TryRedact(string message, out string redacted)
    {
        redacted = message;
        var changed = false;
        foreach (var value in _activeValues.Keys)
        {
            if (redacted.Contains(value, StringComparison.Ordinal))
            {
                redacted = redacted.Replace(value, "[REDACTED-SECRET]", StringComparison.Ordinal);
                changed = true;
            }
        }

        return changed;
    }

    private sealed class Untrack(SecretFingerprintRegistry registry, string value) : IDisposable
    {
        public void Dispose() => registry._activeValues.TryRemove(value, out _);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
