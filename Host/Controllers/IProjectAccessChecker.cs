using System.Security.Claims;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Controllers;

// R-SEC6: shared per-Project visibility check for the frontend-facing REST
// surface -- a global Admin sees everything; anyone else sees only
// Projects they own or hold a ProjectMembership on. Every controller that
// scopes a resource by projectId uses this instead of re-implementing the
// claim-parsing inline (that would risk two controllers drifting apart on
// what "visible" means).
public interface IProjectAccessChecker
{
    Task<bool> CanViewAsync(ClaimsPrincipal user, Guid projectId, CancellationToken ct = default);
}

public sealed class ProjectAccessChecker(IProjectRepository projectRepository) : IProjectAccessChecker
{
    public async Task<bool> CanViewAsync(ClaimsPrincipal user, Guid projectId, CancellationToken ct = default)
    {
        if (user.IsInRole(nameof(Role.Admin)))
            return true;

        var project = await projectRepository.GetByIdAsync(projectId, ct);
        if (project is null)
            return false;

        var userIdClaim = user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (Guid.TryParse(userIdClaim, out var userId) && project.OwnerUserId == userId)
            return true;

        return user.Claims
            .Where(c => c.Type == JwtTokenService.ProjectRoleClaimType)
            .Select(c => c.Value.Split(':', 2)[0])
            .Any(pid => Guid.TryParse(pid, out var claimProjectId) && claimProjectId == projectId);
    }
}
