using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Security.Auth;

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

// R-SEC6: issues/validates the JWT bearer tokens the human-facing REST/Realtime
// API authenticates User sessions with. Project-scoped Role claims come from
// ProjectMembership (R-DM15) — Personas/Agents never receive tokens through
// this path (that's R-SEC2's separate mTLS/signed-token channel).
public interface IJwtTokenService
{
    IssuedToken IssueAccessToken(User user, IReadOnlyList<ProjectMembership> memberships);
}
