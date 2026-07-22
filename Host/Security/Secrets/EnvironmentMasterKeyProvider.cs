using Telechron.Sdk.Security;

namespace Telechron.Host.Security.Secrets;

// R-SEC9: master key sourced from an environment variable (base64, 256-bit) or
// a file path outside the DB — never from SQLite. Swappable later for a cloud
// KMS/HSM-backed IMasterKeyProvider behind the same interface; nothing above
// this layer needs to change when that happens.
//
// Key rotation: setting TELECHRON_MASTER_KEY_ID to a new value alongside a new
// key activates it as CurrentKeyId for future wraps, while GetKey(oldId) must
// still resolve prior keys (via TELECHRON_MASTER_KEY_{oldId}) so existing
// wrapped DEKs remain decryptable until rewrapped.
public sealed class EnvironmentMasterKeyProvider : IMasterKeyProvider
{
    private const string DefaultKeyId = "v1";
    private const string KeyEnvVar = "TELECHRON_MASTER_KEY";
    private const string KeyFileEnvVar = "TELECHRON_MASTER_KEY_FILE";
    private const string KeyIdEnvVar = "TELECHRON_MASTER_KEY_ID";

    private readonly Dictionary<string, byte[]> _keysById;

    public string CurrentKeyId { get; }

    public EnvironmentMasterKeyProvider()
    {
        CurrentKeyId = Environment.GetEnvironmentVariable(KeyIdEnvVar) is { Length: > 0 } configuredId
            ? configuredId
            : DefaultKeyId;

        _keysById = new Dictionary<string, byte[]>
        {
            [CurrentKeyId] = LoadKey(CurrentKeyId),
        };

        // Any additional TELECHRON_MASTER_KEY_{keyId} env vars are prior KEK
        // versions retained for decrypting not-yet-rewrapped DEKs. Excludes the
        // reserved _ID/_FILE suffixes, which are config, not key material.
        var reservedSuffixedVars = new[] { KeyIdEnvVar, KeyFileEnvVar };
        foreach (var (name, value) in Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                     .Select(e => ((string)e.Key, (string?)e.Value))
                     .Where(e => e.Item1.StartsWith($"{KeyEnvVar}_", StringComparison.Ordinal)
                         && !reservedSuffixedVars.Contains(e.Item1)))
        {
            var keyId = name[(KeyEnvVar.Length + 1)..];
            if (!_keysById.ContainsKey(keyId) && value is { Length: > 0 })
                _keysById[keyId] = Convert.FromBase64String(value);
        }
    }

    public ReadOnlyMemory<byte> GetKey(string keyId) =>
        _keysById.TryGetValue(keyId, out var key)
            ? key
            : throw new InvalidOperationException(
                $"No master key available for key ID '{keyId}'. Set {KeyEnvVar}_{keyId} to supply a retired key for decryption.");

    public ReadOnlyMemory<byte> GetCurrentKey() => GetKey(CurrentKeyId);

    private static byte[] LoadKey(string keyId)
    {
        var direct = Environment.GetEnvironmentVariable(KeyEnvVar);
        if (direct is { Length: > 0 })
            return Convert.FromBase64String(direct);

        var filePath = Environment.GetEnvironmentVariable(KeyFileEnvVar);
        if (filePath is { Length: > 0 })
            return Convert.FromBase64String(File.ReadAllText(filePath).Trim());

        throw new InvalidOperationException(
            $"No master key configured. Set {KeyEnvVar} (base64 256-bit key) or {KeyFileEnvVar} " +
            $"(path to a file containing the base64 key) before starting the Host. " +
            $"Key ID resolved as '{keyId}' via {KeyIdEnvVar}.");
    }
}
