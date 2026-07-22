using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Security.Auth;

// R-SEC6: resolves the Project scope from the current request's route values
// (":projectId") and checks the caller's project-scoped Role claim against
// the requirement. A global Admin role satisfies any project requirement
// without needing an explicit membership claim.
public sealed class ProjectRoleAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<ProjectRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ProjectRoleRequirement requirement)
    {
        if (context.User.IsInRole(nameof(Role.Admin)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var httpContext = httpContextAccessor.HttpContext;
        var projectIdValue = httpContext?.Request.RouteValues["projectId"]?.ToString();
        if (projectIdValue is null || !Guid.TryParse(projectIdValue, out var projectId))
        {
            // No project in scope for this route — a project-scoped requirement
            // cannot be satisfied without one. Deny by default (R-MOD8a's
            // spirit: never assume authorization from mere presence of a token).
            return Task.CompletedTask;
        }

        var matchingRoles = context.User.Claims
            .Where(c => c.Type == JwtTokenService.ProjectRoleClaimType)
            .Select(c => c.Value.Split(':', 2))
            .Where(parts => parts.Length == 2 && parts[0] == projectId.ToString())
            .Select(parts => Enum.TryParse<Role>(parts[1], out var role) ? (Role?)role : null)
            .Where(role => role is not null)
            .Select(role => role!.Value)
            .ToList();

        if (matchingRoles.Count > 0 && matchingRoles.Max() >= requirement.MinimumRole)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
