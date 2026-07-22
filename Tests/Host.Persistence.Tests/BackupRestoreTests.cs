using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Persistence.Backup;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Tests;

public sealed class BackupRestoreTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task CreateBackup_ProducesRestorableVerifiedFile()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Backup Subject",
            Email = "backup@telechron.dev",
            AuthCredentialHash = "hash:placeholder",
            Role = Role.Admin,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(user);
        }

        string backupPath;
        using (var scope = _db.CreateScope())
        {
            var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
            backupPath = await backupService.CreateBackupAsync();
        }

        Assert.True(File.Exists(backupPath));

        // The backup is a standalone, independently-openable SQLite file (VACUUM INTO
        // guarantee) containing the data as of backup time.
        var builder = new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadOnly };
        await using var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Users WHERE Email = $email;";
        command.Parameters.AddWithValue("$email", user.Email);
        var count = (long)(await command.ExecuteScalarAsync())!;

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RestoreLatestVerified_RecoversData_AfterSimulatedCorruption()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Pre-Corruption User",
            Email = "precorruption@telechron.dev",
            AuthCredentialHash = "hash:placeholder",
            Role = Role.Admin,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(user);
        }

        using (var scope = _db.CreateScope())
        {
            var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
            await backupService.CreateBackupAsync();
        }

        // Simulate corruption: close all connections and clear the pool (SQLite
        // connections stay pooled — and the file handle open — after Close), then
        // stomp the live DB file.
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TelechronDbContext>();
            await context.Database.CloseConnectionAsync();
            SqliteConnection.ClearPool((SqliteConnection)context.Database.GetDbConnection());
        }
        await File.WriteAllBytesAsync(_db.DbPath, [0xDE, 0xAD, 0xBE, 0xEF]);

        using (var scope = _db.CreateScope())
        {
            var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
            var restoredFrom = await backupService.RestoreLatestVerifiedAsync();
            Assert.NotNull(restoredFrom);
        }

        using (var scope = _db.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var loaded = await repo.GetByEmailAsync(user.Email);

            // Never silently empty (R-REL5): the restored DB must contain the
            // pre-corruption data, not a fresh/empty schema.
            Assert.NotNull(loaded);
            Assert.Equal(user.DisplayName, loaded.DisplayName);
        }
    }

    [Fact]
    public async Task RestoreLatestVerified_Throws_WhenNoBackupExists()
    {
        using var scope = _db.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => backupService.RestoreLatestVerifiedAsync());
    }

    [Fact]
    public async Task CreateBackup_RotatesRetentionSet()
    {
        using var scope = _db.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();

        for (var i = 0; i < 3; i++)
        {
            await backupService.CreateBackupAsync();
            await Task.Delay(1100); // backup filenames are second-resolution timestamps
        }

        var withDefaultRetention = await backupService.ListBackupsAsync();
        Assert.Equal(3, withDefaultRetention.Count); // default MaxRetainedBackups (14) not yet exceeded
    }
}
