using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Security.Audit;
using Telechron.Host.Security.Tests.Fixtures;
using Telechron.Sdk.Security.Audit;

namespace Telechron.Host.Security.Tests.Audit;

public sealed class SqliteAuditLogTests : IAsyncLifetime
{
    private SqliteAuditTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteAuditTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Append_ThenRead_RoundTrips()
    {
        using var scope = _db.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<IAuditLog>();

        var appended = await log.AppendAsync(AuditEventKind.SecretAccessed, """{"handle":"sec_abc"}""");
        var events = await log.ReadAsync();

        Assert.Single(events);
        Assert.Equal(appended.Sequence, events[0].Sequence);
        Assert.Equal(AuditEventKind.SecretAccessed, events[0].Kind);
    }

    [Fact]
    public async Task Chain_VerifiesIntact_ForUnmodifiedAppends()
    {
        using var scope = _db.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<IAuditLog>();

        for (var i = 0; i < 5; i++)
            await log.AppendAsync(AuditEventKind.SecretAccessed, $$"""{"i":{{i}}}""");

        var verification = await log.VerifyChainAsync();

        Assert.True(verification.IsIntact);
        Assert.Null(verification.FirstTamperedSequence);
    }

    [Fact]
    public async Task Chain_DetectsTampering_WhenARowIsEditedDirectly()
    {
        using (var scope = _db.CreateScope())
        {
            var log = scope.ServiceProvider.GetRequiredService<IAuditLog>();
            for (var i = 0; i < 3; i++)
                await log.AppendAsync(AuditEventKind.SecretAccessed, $$"""{"i":{{i}}}""");
        }

        // Simulate an attacker reaching the DB directly and editing a row's
        // content without going through IAuditLog.Append.
        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var row = await context.AuditEvents.OrderBy(a => a.Sequence).Skip(1).FirstAsync();
            row.DetailJson = """{"i":"tampered"}""";
            await context.SaveChangesAsync();
        }

        using (var scope = _db.CreateScope())
        {
            var log = scope.ServiceProvider.GetRequiredService<IAuditLog>();
            var verification = await log.VerifyChainAsync();

            Assert.False(verification.IsIntact);
            Assert.NotNull(verification.FirstTamperedSequence);
        }
    }

    [Fact]
    public async Task Chain_DetectsDeletion_OfAnEarlierRow()
    {
        using (var scope = _db.CreateScope())
        {
            var log = scope.ServiceProvider.GetRequiredService<IAuditLog>();
            for (var i = 0; i < 3; i++)
                await log.AppendAsync(AuditEventKind.SecretAccessed, $$"""{"i":{{i}}}""");
        }

        using (var scope = _db.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var row = await context.AuditEvents.OrderBy(a => a.Sequence).FirstAsync();
            context.AuditEvents.Remove(row);
            await context.SaveChangesAsync();
        }

        using (var scope = _db.CreateScope())
        {
            var log = scope.ServiceProvider.GetRequiredService<IAuditLog>();
            var verification = await log.VerifyChainAsync();

            Assert.False(verification.IsIntact);
        }
    }

    [Fact]
    public async Task ConcurrentAppends_ProduceAValidChain()
    {
        using var scope = _db.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<IAuditLog>();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            log.AppendAsync(AuditEventKind.SecretAccessed, $$"""{"i":{{i}}}""")));

        var verification = await log.VerifyChainAsync();
        var events = await log.ReadAsync(limit: 100);

        Assert.True(verification.IsIntact);
        Assert.Equal(20, events.Count);
    }
}
