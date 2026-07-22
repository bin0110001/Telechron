using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telechron.Host.Persistence;
using Telechron.Host.Persistence.Backup;
using Telechron.Host.Persistence.Repositories;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Tests.Fixtures;

// Real file-backed SQLite DB per test instance (not :memory:) — WAL mode and
// VACUUM INTO both require an actual file on disk to be meaningfully tested.
public sealed class SqliteTestDatabase : IAsyncDisposable
{
    public string DbPath { get; }
    public string BackupDirectory { get; }
    private readonly ServiceProvider _provider;

    public SqliteTestDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), "telechron-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        DbPath = Path.Combine(root, "telechron.db");
        BackupDirectory = Path.Combine(root, "backups");

        var services = new ServiceCollection();
        services.AddTelechronPersistence(DbPath, BackupDirectory);
        services.AddLogging();
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TelechronDbContext>().Database.Migrate();
    }

    public IServiceScope CreateScope() => _provider.CreateScope();

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        var root = Path.GetDirectoryName(DbPath)!;
        try { Directory.Delete(root, recursive: true); } catch { /* best-effort cleanup */ }
    }
}
