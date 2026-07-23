using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence.Backup;
using Telechron.Host.Persistence.Repositories;
using Telechron.Host.Persistence.Retention;
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
        services.AddScoped<IMachineRepository, MachineRepository>();
        services.AddScoped<ILlmConnectionRepository, LlmConnectionRepository>();
        services.AddScoped<IToolchainRepository, ToolchainRepository>();
        services.AddScoped<IFunctionRepository, FunctionRepository>();
        services.AddScoped<IModuleRepository, ModuleRepository>();
        services.AddScoped<IConnectorRepository, ConnectorRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<IRunRepository, RunRepository>();
        services.AddScoped<IPersonaRepository, PersonaRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IWorkflowRunRepository, WorkflowRunRepository>();
        services.AddScoped<IFindingRepository, FindingRepository>();
        services.AddScoped<IIntentPlanRepository, IntentPlanRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<IRepairAttemptRepository, RepairAttemptRepository>();
        services.AddScoped<IDesignDocumentRepository, DesignDocumentRepository>();
        services.AddScoped<IRequirementRepository, RequirementRepository>();
        services.AddScoped<IRequirementRevisionRepository, RequirementRevisionRepository>();

        services.Configure<DatabaseBackupOptions>(o => o.BackupDirectory = backupDirectory);
        services.AddScoped<IDatabaseBackupService, SqliteDatabaseBackupService>();

        return services;
    }

    public static IServiceCollection AddTelechronScheduledBackups(this IServiceCollection services)
    {
        services.AddHostedService<ScheduledBackupHostedService>();
        return services;
    }

    // R-PER7: Artifact binary payloads live outside SQLite.
    public static IServiceCollection AddTelechronArtifactBlobStore(this IServiceCollection services, string rootDirectory)
    {
        services.AddSingleton<Sdk.Persistence.IArtifactBlobStore>(new FilesystemArtifactBlobStore(rootDirectory));
        return services;
    }

    // R-PER7: age/count retention with archival-before-delete for Runs/Findings.
    public static IServiceCollection AddTelechronRetention(this IServiceCollection services, string archiveRootDirectory)
    {
        services.AddSingleton<IRetentionArchive>(new FilesystemRetentionArchive(archiveRootDirectory));
        services.AddScoped<RetentionPass>();
        return services;
    }

    public static IServiceCollection AddTelechronScheduledRetention(this IServiceCollection services)
    {
        services.AddHostedService<ScheduledRetentionHostedService>();
        return services;
    }
}
