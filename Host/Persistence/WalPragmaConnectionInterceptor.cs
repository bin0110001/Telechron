using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Telechron.Host.Persistence;

// R-PER1: puts every connection into WAL mode and sets busy_timeout as soon
// as it opens, so readers never block writers and short lock contention waits
// at the SQLite layer before EF's retry strategy (SqliteRetryingExecutionStrategy)
// takes over.
public sealed class WalPragmaConnectionInterceptor(int busyTimeoutMilliseconds) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        ApplyPragmas((SqliteConnection)connection);

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await ApplyPragmasAsync((SqliteConnection)connection, cancellationToken);
    }

    private void ApplyPragmas(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA journal_mode=WAL; PRAGMA busy_timeout={busyTimeoutMilliseconds};";
        command.ExecuteNonQuery();
    }

    private async Task ApplyPragmasAsync(SqliteConnection connection, CancellationToken ct)
    {
        var command = connection.CreateCommand();
        await using var _ = command.ConfigureAwait(false);
        command.CommandText = $"PRAGMA journal_mode=WAL; PRAGMA busy_timeout={busyTimeoutMilliseconds};";
        await command.ExecuteNonQueryAsync(ct);
    }
}
