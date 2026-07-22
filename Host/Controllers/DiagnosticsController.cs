using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Telechron.Host.Security.Auth;

namespace Telechron.Host.Controllers;

// Minimal authenticated/RBAC-gated surface used to exercise the R-SEC6 seam
// end-to-end (both by tests and manually) before Phase 3+ controllers exist.
[ApiController]
[Route("api/diagnostics")]
public sealed class DiagnosticsController : ControllerBase
{
    [HttpGet("whoami")]
    [Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireViewer)]
    public IActionResult WhoAmI() => Ok(new { name = User.Identity?.Name });

    [HttpPost("{projectId:guid}/operator-only")]
    [Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireOperator)]
    [EnableRateLimiting(RateLimiting.MutatingPolicyName)]
    public IActionResult OperatorOnly(Guid projectId) => Ok(new { projectId });

    [HttpPost("{projectId:guid}/admin-only")]
    [Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireAdmin)]
    [EnableRateLimiting(RateLimiting.MutatingPolicyName)]
    public IActionResult AdminOnly(Guid projectId) => Ok(new { projectId });
}
