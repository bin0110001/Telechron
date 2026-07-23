using System.Security.Cryptography;

namespace Telechron.Host.Agents;

// R-SEC2: session tokens are opaque random bearer strings, never JWTs — the
// Host is the only party that ever needs to validate one (a straight hash
// lookup against AgentSession), so there's no benefit to a self-describing
// token format here, only downside (JWTs can't be revoked without a
// denylist, which is exactly the lookup we're already doing).
public static class AgentSessionTokenService
{
    private const int TokenSizeBytes = 32;

    public static string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenSizeBytes));

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
