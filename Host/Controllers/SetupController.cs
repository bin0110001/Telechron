using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Security.Audit;

namespace Telechron.Host.Controllers;

public sealed record SetupStatusResponse(bool IsSetupComplete);
public sealed record CreateFirstAdminRequest(string SetupToken, string Email, string Password, string DisplayName);
public sealed record CreateFirstAdminResponse(Guid UserId, string Email);

// R-SEC6: the only path that can create a User before any Admin exists.
// No public self-registration endpoint exists anywhere else in the API --
// this one is intentionally narrow: it requires an out-of-band setup
// token (never a hardcoded default, see SetupOptions) AND refuses to run
// if even one User already exists, so a leaked/reused token can't be used
// to mint a second privileged account later. Not [Authorize]-gated,
// because by definition nobody can be authenticated yet the first time
// this legitimately needs to run.
[ApiController]
[Route("api/setup")]
public sealed class SetupController(
    IUserRepository userRepository, PasswordHashing passwordHashing, IOptions<SetupOptions> setupOptions, IAuditLog auditLog) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<SetupStatusResponse>> GetStatusAsync(CancellationToken ct)
    {
        var anyRealUsersExist = await AnyRealUserExistsAsync(ct);
        return Ok(new SetupStatusResponse(anyRealUsersExist));
    }

    [HttpPost("first-admin")]
    [EnableRateLimiting(RateLimiting.AuthPolicyName)]
    public async Task<ActionResult<CreateFirstAdminResponse>> CreateFirstAdminAsync(
        [FromBody] CreateFirstAdminRequest request, CancellationToken ct)
    {
        var configuredToken = setupOptions.Value.SetupToken;
        if (string.IsNullOrEmpty(configuredToken))
            return Problem("Setup is disabled: TELECHRON_SETUP_TOKEN is not configured on the Host.", statusCode: StatusCodes.Status503ServiceUnavailable);

        // Constant-time comparison -- same pattern as agent enrollment
        // token validation (AgentServiceImpl.CryptographicallyEqual): this
        // is a bearer secret compared against attacker-controlled input,
        // so a naive == comparison would leak timing information about
        // how many leading characters matched.
        if (!CryptographicallyEqual(request.SetupToken, configuredToken))
            return Unauthorized("Invalid setup token.");

        if (await AnyRealUserExistsAsync(ct))
            return Conflict("Setup has already been completed -- a User already exists. Use /api/auth/login instead.");

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest("Email, Password, and DisplayName are all required.");
        if (request.Password.Length < 12)
            return BadRequest("Password must be at least 12 characters.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = request.DisplayName,
            Email = request.Email,
            AuthCredentialHash = string.Empty,
            Role = Role.Admin,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        user = user with { AuthCredentialHash = passwordHashing.Hash(user, request.Password) };
        await userRepository.AddAsync(user, ct);

        await auditLog.AppendAsync(
            AuditEventKind.SystemBootstrapped,
            System.Text.Json.JsonSerializer.Serialize(new { userId = user.Id, email = user.Email }),
            actorUserId: user.Id,
            ct: ct);

        return Ok(new CreateFirstAdminResponse(user.Id, user.Email));
    }

    // ReflexiveDesignDocumentSeeder creates a non-loginable "Telechron
    // System" User (owner of the self-repair Project) on every startup,
    // with AuthCredentialHash left permanently empty since it's never a
    // human-loginable account. PasswordHashing.Hash never produces an
    // empty hash for a real password (CreateFirstAdminAsync always sets
    // one before persisting), so this is a stable way to tell "the system
    // seed ran" apart from "a real Admin was bootstrapped" without adding
    // schema just for this distinction.
    private async Task<bool> AnyRealUserExistsAsync(CancellationToken ct)
    {
        var users = await userRepository.GetAllAsync(ct);
        return users.Any(u => !string.IsNullOrEmpty(u.AuthCredentialHash));
    }

    private static bool CryptographicallyEqual(string a, string b)
    {
        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
