using Microsoft.AspNetCore.Authorization;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Security.Auth;

// R-SEC6: per-Project RBAC requirement — satisfied when the authenticated
// User's ProjectRoleClaimType claim for the route's {projectId} meets or
// exceeds MinimumRole. A global Admin (ClaimTypes.Role) always satisfies any
// project requirement, since Admin is the top of the Role ordering (R-DM15).
public sealed class ProjectRoleRequirement(Role minimumRole) : IAuthorizationRequirement
{
    public Role MinimumRole { get; } = minimumRole;
}
