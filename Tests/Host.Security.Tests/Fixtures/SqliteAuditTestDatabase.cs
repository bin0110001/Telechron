using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Security.Audit;

namespace Telechron.Host.Security.Tests.Fixtures;

public sealed class SqliteAuditTestDatabase : IAsyncDisposable
{
    public string DbPath { get; }
    private readonly ServiceProvider _provider;

    public SqliteAuditTestDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), "telechron-security-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        DbPath = Path.Combine(root, "audit.db");

        var services = new ServiceCollection();
        services.AddTelechronAuditLog(DbPath);
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.Migrate();
    }

    public IServiceScope CreateScope() => _provider.CreateScope();

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        var root = Path.GetDirectoryName(DbPath)!;
        try { Directory.Delete(root, recursive: true); } catch { /* best-effort cleanup */ }
    }
}
