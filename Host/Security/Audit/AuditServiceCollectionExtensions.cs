using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Telechron.Host.Persistence;
using Telechron.Sdk.Security.Audit;

namespace Telechron.Host.Security.Audit;

public static class AuditServiceCollectionExtensions
{
    // R-SEC7: dataSource must be a distinct SQLite file from the operational
    // TelechronDbContext database — physically separate storage is the point.
    public static IServiceCollection AddTelechronAuditLog(this IServiceCollection services, string dataSource)
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = dataSource };

        services.AddDbContext<AuditDbContext>(options =>
        {
            options.UseSqlite(connectionStringBuilder.ConnectionString);
            options.AddInterceptors(new WalPragmaConnectionInterceptor(busyTimeoutMilliseconds: 5000));
        });

        services.AddScoped<IAuditLog, SqliteAuditLog>();

        return services;
    }
}
