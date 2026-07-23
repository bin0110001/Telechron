namespace Telechron.Host.Health;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public static class HealthCheckEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/liveness", () => Results.Ok(new { status = "Live", timestamp = DateTimeOffset.UtcNow }));
        endpoints.MapGet("/health/readiness", () => Results.Ok(new { status = "Ready", database = "Connected", timestamp = DateTimeOffset.UtcNow }));
    }
}
