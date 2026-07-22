using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Security.Tests.Fixtures;
using Telechron.Sdk.Security;

namespace Telechron.Host.Security.Tests.SecretVault;

// R-SEC5: proves a simulated agentic connector call authenticates using a
// resolved secret without that secret ever appearing in the "tool-call args"
// or "tool output" the Persona/LLM would see.
public sealed class SecretResolutionScopeTests : IAsyncLifetime
{
    private SecretVaultTestFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new SecretVaultTestFixture();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task SimulatedConnectorCall_AuthenticatesWithoutSecretInToolArgsOrOutput()
    {
        using var scope = _fixture.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var resolutionScope = scope.ServiceProvider.GetRequiredService<ISecretResolutionScope>();

        var rawApiKey = "sk-live-1234567890abcdef";
        var handle = await vault.StoreAsync(await scope.SeedProjectAsync(), "API Key", Encoding.UTF8.GetBytes(rawApiKey));

        // Simulates a Persona's tool-call construction step: it only ever
        // constructs a "tool-call args" object containing the handle.
        var simulatedToolCallArgs = new { endpoint = "https://api.example.com/widgets", apiKeyHandle = handle };
        Assert.DoesNotContain(rawApiKey, simulatedToolCallArgs.apiKeyHandle);

        // The final hop — inside the Host/Connector runtime — is the only place
        // the raw value is ever available.
        var simulatedHttpAuthHeaderSeenByServer = string.Empty;
        var simulatedResponseBody = await resolutionScope.ExecuteAsync(handle, async rawValue =>
        {
            simulatedHttpAuthHeaderSeenByServer = $"Bearer {Encoding.UTF8.GetString(rawValue.Span)}";
            await Task.CompletedTask;
            // Simulated tool result — must never echo the raw key back.
            return """{"status":"ok","widgets":[]}""";
        });

        Assert.Equal($"Bearer {rawApiKey}", simulatedHttpAuthHeaderSeenByServer); // the real call did authenticate
        Assert.DoesNotContain(rawApiKey, simulatedResponseBody, StringComparison.Ordinal); // but it never leaks back out
        Assert.DoesNotContain(rawApiKey, simulatedToolCallArgs.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScrubForPromptReentry_RedactsHandleFromToolResult()
    {
        using var scope = _fixture.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var resolutionScope = scope.ServiceProvider.GetRequiredService<ISecretResolutionScope>();
        var handle = await vault.StoreAsync(await scope.SeedProjectAsync(), "Test", Encoding.UTF8.GetBytes("value"));

        var toolResultEchoingHandle = $"Request completed using credential {handle}.";
        var scrubbed = resolutionScope.ScrubForPromptReentry(toolResultEchoingHandle, [handle]);

        Assert.DoesNotContain(handle, scrubbed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RawValue_IsZeroedInPlaceAfterScopeCompletes()
    {
        using var scope = _fixture.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var resolutionScope = scope.ServiceProvider.GetRequiredService<ISecretResolutionScope>();
        var handle = await vault.StoreAsync(await scope.SeedProjectAsync(), "Test", Encoding.UTF8.GetBytes("value-to-zero"));

        Memory<byte> capturedMemory = default;
        await resolutionScope.ExecuteAsync(handle, async rawValue =>
        {
            capturedMemory = MemoryMarshal.AsMemory(rawValue);
            await Task.CompletedTask;
            return 0;
        });

        // ExecuteAsync's finally block clears the same backing array the
        // callback was handed — asserting on that array post-scope confirms
        // the raw value doesn't linger in memory once the final hop returns.
        Assert.All(capturedMemory.ToArray(), b => Assert.Equal(0, b));
    }
}
