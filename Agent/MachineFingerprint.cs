using System.Security.Cryptography;
using System.Text;

namespace Telechron.Agent;

// R-SCH3: a stable per-machine identifier the Host uses to dedupe
// re-registration. Derived from OS + machine name rather than any single
// hardware ID, since not every platform exposes a reliable one uniformly —
// stable enough for dev/single-tenant use; a production Agent would prefer a
// TPM-backed or OS-provisioned machine ID where available.
public static class MachineFingerprint
{
    public static string Compute()
    {
        var raw = $"{Environment.MachineName}|{Environment.OSVersion.Platform}|{Environment.ProcessorCount}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexStringLower(hash);
    }
}
