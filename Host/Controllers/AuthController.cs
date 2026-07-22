using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Security.Audit;
using Telechron.Sdk.Security.Auth;

namespace Telechron.Host.Controllers;

public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);

// R-SEC6: human-facing session issuance. Rate limited (EnableRateLimiting) to
// blunt credential-stuffing; failures are audited (R-SEC7) without ever
// logging the submitted password.
[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimiting.AuthPolicyName)]
public sealed class AuthController(
    IUserRepository userRepository,
    IProjectMembershipRepository membershipRepository,
    PasswordHashing passwordHashing,
    IJwtTokenService tokenService,
    IAuditLog auditLog) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Email and password are required.");

        var user = await userRepository.GetByEmailAsync(request.Email, ct);
        if (user is null || !passwordHashing.Verify(user, user.AuthCredentialHash, request.Password))
        {
            await auditLog.AppendAsync(
                AuditEventKind.AuthenticationFailed,
                System.Text.Json.JsonSerializer.Serialize(new { email = request.Email }),
                ct: ct);
            return Unauthorized();
        }

        var memberships = await membershipRepository.GetByUserAsync(user.Id, ct);
        var issued = tokenService.IssueAccessToken(user, memberships);

        return Ok(new LoginResponse(issued.AccessToken, issued.ExpiresAtUtc));
    }
}
