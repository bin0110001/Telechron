using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Llm;
using Telechron.Host.Modules.Runtime;
using Telechron.Host.Modules.Tests.Fixtures;
using Telechron.Modules.OllamaEngine;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Modules.Llm;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Modules.Tests;

// R-LLM1/R-LLM3/R-LLM4 end to end: real ModuleRuntime-backed provider
// resolution, real (SQLite-backed) call tracking, real spend-cap
// enforcement, dispatched against the actual local Ollama instance. Skips
// if Ollama isn't reachable.
public sealed class LlmDispatcherTests : IAsyncLifetime
{
    private bool _ollamaAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await client.GetAsync("http://localhost:11434/api/tags");
            _ollamaAvailable = response.IsSuccessStatusCode;
        }
        catch
        {
            _ollamaAvailable = false;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static void RegisterOllamaModule(IModuleRuntime runtime)
    {
        var field = runtime.GetType().GetField("_loaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<string, LoadedModule>)field.GetValue(runtime)!;
        var instance = new OllamaEngineModule();
        dict["telechron.llm.ollama"] = new LoadedModule
        {
            ModuleName = "telechron.llm.ollama",
            Version = instance.Version,
            Instance = instance,
            LoadContext = new ModuleLoadContext(typeof(OllamaEngineModule).Assembly.Location),
            UnloadWeakReference = new WeakReference(new object()),
            LoadedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    [SkippableFact]
    public async Task DispatchAsync_RealOllamaConnection_TracksCallAndReturnsRealResponse()
    {
        Skip.IfNot(_ollamaAvailable, "Ollama is not reachable at http://localhost:11434.");

        await using var fixture = new LlmDispatcherTestFixture(
            configureProviders: o => o.ProviderToModuleName["ollama"] = "telechron.llm.ollama");

        using var scope = fixture.CreateScope();
        var moduleRuntime = scope.ServiceProvider.GetRequiredService<IModuleRuntime>();
        RegisterOllamaModule(moduleRuntime);

        var connection = new LlmConnection
        {
            Id = Guid.NewGuid(), Name = "test-ollama", Provider = "ollama",
            ConfigurationJson = """{"model": "gemma4:latest"}""", SecretHandle = null, CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await scope.ServiceProvider.GetRequiredService<ILlmConnectionRepository>().AddAsync(connection);

        var dispatcher = scope.ServiceProvider.GetRequiredService<ILlmDispatcher>();
        var request = new LlmCompletionRequest(
            "You are a terse test assistant.", "Reply with exactly the single word: OK", [], "gemma4:latest", 0.0, 20);

        var result = await dispatcher.DispatchAsync(connection, projectId: null, request);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotEmpty(result.ResponseText);

        var llmCallRepository = scope.ServiceProvider.GetRequiredService<ILlmCallRepository>();
        var recorded = await llmCallRepository.GetSinceAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        var call = Assert.Single(recorded);
        Assert.Equal("ollama", call.Provider);
        Assert.True(call.PromptTokens > 0);
        Assert.True(call.Succeeded);
    }

    [SkippableFact]
    public async Task DispatchAsync_SpendCapAlreadyExceeded_DeclinesWithoutContactingProvider()
    {
        Skip.IfNot(_ollamaAvailable, "Ollama is not reachable at http://localhost:11434.");

        await using var fixture = new LlmDispatcherTestFixture(
            configureProviders: o => o.ProviderToModuleName["ollama"] = "telechron.llm.ollama",
            configureSpendCaps: o => o.GlobalCapUsd = 0.0000001m); // effectively zero -- any recorded cost trips it

        using var scope = fixture.CreateScope();

        var connection = new LlmConnection
        {
            Id = Guid.NewGuid(), Name = "test-ollama", Provider = "ollama",
            ConfigurationJson = """{"model": "gemma4:latest"}""", SecretHandle = null, CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await scope.ServiceProvider.GetRequiredService<ILlmConnectionRepository>().AddAsync(connection);

        var llmCallRepository = scope.ServiceProvider.GetRequiredService<ILlmCallRepository>();
        await llmCallRepository.AddAsync(new LlmCall
        {
            Id = Guid.NewGuid(), LlmConnectionId = connection.Id, Provider = "ollama", Model = "x",
            PromptTokens = 1, CompletionTokens = 1, EstimatedCostUsd = 1m, Succeeded = true, OccurredAtUtc = DateTimeOffset.UtcNow,
        });

        // Deliberately NOT registering the Ollama module in ModuleRuntime --
        // if the spend cap check didn't short-circuit first, resolving the
        // provider would fail with a different error message, so this also
        // proves ordering (cap check happens before provider resolution).

        var dispatcher = scope.ServiceProvider.GetRequiredService<ILlmDispatcher>();
        var request = new LlmCompletionRequest("sys", "instr", [], "gemma4:latest", 0.0, 20);

        var result = await dispatcher.DispatchAsync(connection, projectId: null, request);

        Assert.False(result.Succeeded);
        Assert.Contains("spend cap", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
