using System.Threading.RateLimiting;

namespace Telechron.Host.Security.Auth;

// R-SEC6: rate limiting on mutating endpoints, applied per-policy so login
// (credential-stuffing target) and general mutations (e.g. approvals, secret
// writes) can have distinct thresholds.
public static class RateLimiting
{
    public const string AuthPolicyName = "auth";
    public const string MutatingPolicyName = "mutating";

    public static IServiceCollection AddTelechronRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AuthPolicyName, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 10,
                        QueueLimit = 0,
                    }));

            options.AddPolicy(MutatingPolicyName, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 60,
                        QueueLimit = 0,
                    }));
        });

        return services;
    }
}
