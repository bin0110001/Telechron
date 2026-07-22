using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Tests;

public sealed class WalModeAndConcurrencyTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Database_OpensInWalJournalMode()
    {
        using var scope = _db.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TelechronDbContext>();
        await context.Database.OpenConnectionAsync();

        var connection = (SqliteConnection)context.Database.GetDbConnection();
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        var mode = (string)(await command.ExecuteScalarAsync())!;

        Assert.Equal("wal", mode, ignoreCase: true);
    }

    [Fact]
    public async Task ConcurrentWrites_RetryAndSucceed_RatherThanHardFail()
    {
        // Hold SQLite's write lock via a raw ADO.NET IMMEDIATE transaction on
        // one connection while a second, independent scope attempts a write
        // through the repository/EF path — this forces a real SQLITE_BUSY
        // that only the retry/backoff execution strategy (R-PER6) can resolve,
        // since busy_timeout alone won't cover a lock held for the test's duration.
        await using var blockingConnection = new SqliteConnection($"Data Source={_db.DbPath}");
        await blockingConnection.OpenAsync();
        var blockingTransaction = (SqliteTransaction)await blockingConnection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var lockCommand = blockingConnection.CreateCommand();
        lockCommand.Transaction = blockingTransaction;
        lockCommand.CommandText = "CREATE TABLE IF NOT EXISTS _lock_probe(x INTEGER); INSERT INTO _lock_probe VALUES (1);";
        await lockCommand.ExecuteNonQueryAsync();

        var contendedWrite = Task.Run(async () =>
        {
            using var scope = _db.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            await repo.AddAsync(NewUser());
        });

        // Release the lock shortly after the contended write starts, so the
        // retry strategy has something real to recover from.
        await Task.Delay(300);
        await blockingTransaction.CommitAsync();

        var exception = await Record.ExceptionAsync(() => contendedWrite);
        Assert.Null(exception);
    }

    private static User NewUser() => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = "Concurrency Test User",
        Email = $"{Guid.NewGuid():N}@telechron.dev",
        AuthCredentialHash = "hash:placeholder",
        Role = Role.Viewer,
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };
}
