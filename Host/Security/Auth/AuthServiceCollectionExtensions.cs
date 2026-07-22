using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Security.Auth;

namespace Telechron.Host.Security.Auth;

public static class AuthServiceCollectionExtensions
{
    // R-SEC6: policy names callers reference via [Authorize(Policy = ...)].
    // One per Role tier — Viewer requires only authentication, Operator+/Admin+
    // require the corresponding project-scoped (or global Admin) role.
    public static class Policies
    {
        public const string RequireViewer = "telechron:require-viewer";
        public const string RequireOperator = "telechron:require-operator";
        public const string RequireAdmin = "telechron:require-admin";
    }

    public static IServiceCollection AddTelechronApiAuth(this IServiceCollection services, JwtOptions jwtOptions)
    {
        if (string.IsNullOrEmpty(jwtOptions.SigningKey))
            throw new InvalidOperationException(
                "JWT signing key not configured. Set TELECHRON_JWT_SIGNING_KEY before starting the Host.");

        services.Configure<JwtOptions>(o =>
        {
            o.SigningKey = jwtOptions.SigningKey;
            o.Issuer = jwtOptions.Issuer;
            o.Audience = jwtOptions.Audience;
            o.AccessTokenLifetime = jwtOptions.AccessTokenLifetime;
        });

        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<PasswordHashing>();
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationHandler, ProjectRoleAuthorizationHandler>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.RequireViewer, p => p.RequireAuthenticatedUser())
            .AddPolicy(Policies.RequireOperator, p => p.AddRequirements(new ProjectRoleRequirement(Role.Operator)))
            .AddPolicy(Policies.RequireAdmin, p => p.AddRequirements(new ProjectRoleRequirement(Role.Admin)));

        return services;
    }

    // R-SEC6: CORS restricted to configured origins — no AllowAnyOrigin, ever.
    public const string CorsPolicyName = "telechron-frontend";

    public static IServiceCollection AddTelechronCors(this IServiceCollection services, IReadOnlyList<string> allowedOrigins) =>
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (allowedOrigins.Count > 0)
                {
                    policy.WithOrigins([.. allowedOrigins])
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
                // No origins configured: policy permits nothing (default-deny),
                // rather than falling back to AllowAnyOrigin.
            });
        });
}
