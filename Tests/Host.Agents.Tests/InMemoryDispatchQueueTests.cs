using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Agents.Dispatch;
using Telechron.Host.Agents.Tests.Fixtures;
using Telechron.Sdk.Agents;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Security.Audit;

namespace Telechron.Host.Agents.Tests;

public sealed class InMemoryDispatchQueueTests : IAsyncLifetime
{
    private AgentServiceTestFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new AgentServiceTestFixture();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void Enqueue_ValidCommand_Succeeds_AndIsReadable()
    {
        using var scope = _fixture.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IDispatchQueue>();
        var machineId = Guid.NewGuid();
        var command = new DispatchedCommand(
            Guid.NewGuid(), Guid.NewGuid(), CommandKinds.RunTests,
            """{"projectRootRelativePath":"src","toolchainName":"dotnet"}""", "sha256:abc");

        var result = queue.Enqueue(machineId, command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Enqueue_InvalidCommand_IsRejected_AndNeverEnqueued()
    {
        using var scope = _fixture.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IDispatchQueue>();
        var machineId = Guid.NewGuid();
        var command = new DispatchedCommand(
            Guid.NewGuid(), Guid.NewGuid(), CommandKinds.RunTests,
            """{"projectRootRelativePath":"; rm -rf /","toolchainName":"dotnet"}""", "sha256:abc");

        var result = queue.Enqueue(machineId, command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Enqueue_InvalidCommand_IsAudited()
    {
        using var scope = _fixture.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IDispatchQueue>();
        var machineId = Guid.NewGuid();
        var command = new DispatchedCommand(
            Guid.NewGuid(), Guid.NewGuid(), "not-a-real-kind", "{}", "sha256:abc");

        queue.Enqueue(machineId, command);

        // Audit append is fire-and-forget (see InMemoryDispatchQueue) — give
        // it a moment to land before asserting.
        await Task.Delay(200);

        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        var events = await auditLog.ReadAsync(limit: 100);
        Assert.Contains(events, e => e.Kind == AuditEventKind.AuthorizationDenied
            && e.DetailJson.Contains("command_dispatch_validation_failed"));
    }

    [Fact]
    public async Task ReadAllAsync_OnlyReceivesCommandsForItsOwnMachine()
    {
        using var scope = _fixture.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IDispatchQueue>();
        var machineA = Guid.NewGuid();
        var machineB = Guid.NewGuid();
        var commandForA = new DispatchedCommand(
            Guid.NewGuid(), Guid.NewGuid(), CommandKinds.RunTests,
            """{"projectRootRelativePath":"src","toolchainName":"dotnet"}""", "sha256:abc");

        queue.Enqueue(machineA, commandForA);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var received = new List<DispatchedCommand>();
        try
        {
            await foreach (var cmd in queue.ReadAllAsync(machineB, cts.Token))
            {
                received.Add(cmd);
            }
        }
        catch (OperationCanceledException) { /* expected — nothing was ever enqueued for machineB */ }

        Assert.Empty(received);
    }
}
