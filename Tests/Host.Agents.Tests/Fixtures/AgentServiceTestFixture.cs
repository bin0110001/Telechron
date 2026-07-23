using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Agents;
using Telechron.Host.Agents.Dispatch;
using Telechron.Host.Agents.Grpc;
using Telechron.Host.Persistence;
using Telechron.Host.Security.Audit;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Security;

namespace Telechron.Host.Agents.Tests.Fixtures;

// Real file-backed SQLite (operational + audit) wired to a real
// AgentServiceImpl, called directly (bypassing the actual gRPC/mTLS
// transport, which is covered by manual live testing per
// ImplementationPlan.md's Phase 4 notes) — this exercises the service's
// business logic: registration dedup, session issuance/validation,
// dispatch-queue wiring.
public sealed class AgentServiceTestFixture : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    public const string EnrollmentToken = "test-enrollment-token";

    public AgentServiceTestFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "telechron-agent-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var services = new ServiceCollection();
        services.AddTelechronPersistence(Path.Combine(root, "telechron.db"), Path.Combine(root, "backups"));
        services.AddTelechronAuditLog(Path.Combine(root, "audit.db"));
        services.AddSingleton<ICommandDispatchValidator, CommandDispatchValidator>();
        services.AddSingleton<IDispatchQueue, InMemoryDispatchQueue>();
        services.Configure<AgentEnrollmentOptions>(o => o.EnrollmentToken = EnrollmentToken);
        services.AddScoped<AgentServiceImpl>();
        services.AddLogging();

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TelechronDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.Migrate();
    }

    public IServiceScope CreateScope() => _provider.CreateScope();

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();
}
