namespace Telechron.Sdk.Security;

// R-SEC1 defense-in-depth: while a raw secret value is live in memory (inside
// SecretVault/ISecretResolutionScope), its fingerprint is registered here so
// the logging redaction safety net (Host.Security.Logging) can catch any
// accidental leak into a log message — an exception embedding a request body,
// a verbose trace, etc. This is a backstop, not the primary control; the
// primary control is that raw values are never passed to ILogger by
// construction.
public interface ISecretFingerprintRegistry
{
    IDisposable Track(ReadOnlySpan<byte> rawValue);

    bool TryRedact(string message, out string redacted);
}
