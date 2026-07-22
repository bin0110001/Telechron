namespace Telechron.Host.Security.Auth;

public sealed class JwtOptions
{
    // Signing key is a separate secret from the master key (R-SEC9) — sourced
    // the same way (env var), but rotating one must not require rotating the
    // other. See TELECHRON_JWT_SIGNING_KEY.
    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "telechron-host";
    public string Audience { get; set; } = "telechron-api";
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);
}
