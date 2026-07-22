using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Storage;

namespace Telechron.Host.Persistence;

// R-PER1/R-PER6: SQLite is single-writer. WAL mode plus busy_timeout (set at
// connection-open time, see ServiceCollectionExtensions) handles short lock
// waits at the SQLite layer; this execution strategy adds an EF-level retry
// on top for SQLITE_BUSY (5) / SQLITE_LOCKED (6) that outlast busy_timeout,
// with exponential backoff, so contention produces a bounded retry instead of
// a hard failure.
public sealed class SqliteRetryingExecutionStrategy(TelechronDbContext context)
    : ExecutionStrategy(context, maxRetryCount: 6, maxRetryDelay: TimeSpan.FromSeconds(2))
{
    private static readonly int[] TransientSqliteErrorCodes = [5, 6]; // SQLITE_BUSY, SQLITE_LOCKED

    protected override bool ShouldRetryOn(Exception exception) =>
        exception is SqliteException sqliteEx && TransientSqliteErrorCodes.Contains(sqliteEx.SqliteErrorCode);
}
