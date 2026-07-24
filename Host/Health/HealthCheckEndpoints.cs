namespace Telechron.Host.Health;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence;

// R-REL6: liveness/readiness endpoints the watchdog/sentinel and any
// external orchestrator poll. Readiness performs a real database round
// trip -- a health endpoint that always reports "Connected" regardless of
// actual DB state cannot ever signal an unhealthy Host, defeating its
// purpose.
public static class HealthCheckEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/liveness", () => Results.Ok(new { status = "Live", timestamp = DateTimeOffset.UtcNow }));

        endpoints.MapGet("/health/readiness", async (TelechronDbContext db, CancellationToken ct) =>
        {
            bool databaseReachable;
            try
            {
                databaseReachable = await db.Database.CanConnectAsync(ct);
            }
            catch (Exception)
            {
                databaseReachable = false;
            }

            var status = databaseReachable ? "Ready" : "Unavailable";
            var response = new { status, database = databaseReachable ? "Connected" : "Unreachable", timestamp = DateTimeOffset.UtcNow };
            return databaseReachable ? Results.Ok(response) : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
        });
    }
}
