using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Security.Auth;

namespace Telechron.Host.Security.Auth;

// R-SEC6: project-scoped Role claims (ClaimTypes.Role is the User's global
// Role; ProjectRoleClaimType-prefixed claims carry per-Project RBAC from
// ProjectMembership) so authorization policies can check either.
public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    public const string ProjectRoleClaimType = "telechron:project_role";

    public IssuedToken IssueAccessToken(User user, IReadOnlyList<ProjectMembership> memberships)
    {
        var opts = options.Value;
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(opts.AccessTokenLifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        claims.AddRange(memberships.Select(m => new Claim(ProjectRoleClaimType, $"{m.ProjectId}:{m.Role}")));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: opts.Issuer,
            audience: opts.Audience,
            claims: claims,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
