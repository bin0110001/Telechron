using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Backup;
using Telechron.Host.Persistence.Repositories;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    // Default busy_timeout (ms) SQLite waits on a locked DB before returning
    // SQLITE_BUSY, ahead of the EF-level retry strategy taking over (R-PER6).
    private const int BusyTimeoutMilliseconds = 5000;

    public static IServiceCollection AddTelechronPersistence(
        this IServiceCollection services, string dataSource, string backupDirectory)
    {
        // WAL mode itself is set per-connection via PRAGMA in WalPragmaConnectionInterceptor
        // (SQLitePCLRaw connection strings don't expose a journal-mode property).
        var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = dataSource };

        services.AddDbContext<TelechronDbContext>((_, options) =>
        {
            options.UseSqlite(connectionStringBuilder.ConnectionString, sqlite =>
            {
                sqlite.CommandTimeout((int)TimeSpan.FromSeconds(30).TotalSeconds);
            });
            options.AddInterceptors(new WalPragmaConnectionInterceptor(BusyTimeoutMilliseconds));
            options.ReplaceService<Microsoft.EntityFrameworkCore.Storage.IExecutionStrategyFactory, SqliteRetryingExecutionStrategyFactory>();
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISecretRepository, SecretRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectMembershipRepository, ProjectMembershipRepository>();

        services.Configure<DatabaseBackupOptions>(o => o.BackupDirectory = backupDirectory);
        services.AddScoped<IDatabaseBackupService, SqliteDatabaseBackupService>();

        return services;
    }

    public static IServiceCollection AddTelechronScheduledBackups(this IServiceCollection services)
    {
        services.AddHostedService<ScheduledBackupHostedService>();
        return services;
    }
}
