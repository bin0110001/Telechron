using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Persistence;
using Telechron.Host.Security.Audit;

namespace Telechron.Host.Security.Tests.Fixtures;

// Boots the real Host (Program.cs) end-to-end against a temp data directory
// and a fixed test JWT signing key, so auth/RBAC/CORS/rate-limit tests
// exercise the actual middleware pipeline rather than isolated units.
public sealed class TelechronApiFactory : WebApplicationFactory<Program>
{
    public readonly string DataDirectory =
        Path.Combine(Path.GetTempPath(), "telechron-security-tests", "api-" + Guid.NewGuid().ToString("N"));

    public const string JwtSigningKey = "test-signing-key-at-least-32-bytes-long-for-hmac-sha256!!";
    public const string AllowedOrigin = "https://allowed.example.com";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(DataDirectory);
        Environment.SetEnvironmentVariable("TELECHRON_MASTER_KEY", Convert.ToBase64String(new byte[32]));

        builder.UseSetting("Telechron:DataDirectory", DataDirectory);
        builder.UseSetting("Telechron:JwtSigningKey", JwtSigningKey);
        builder.UseSetting("Telechron:AllowedOrigins", AllowedOrigin);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(DataDirectory, recursive: true); } catch { /* best-effort cleanup */ }
    }
}
