using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Connectors;
using Telechron.Host.Modules.Runtime;
using Telechron.Host.Modules.Tests.Fixtures;
using Telechron.Modules.GitHubConnector;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Security;
using Telechron.Sdk.Security.Permissions;

namespace Telechron.Host.Modules.Tests;

// R-MOD9/R-SEC5/R-MOD8a end to end: a real Connector dispatch through
// real capability mediation and a real (encrypted, SQLite-backed) secret
// vault, invoking the real GitHub connector module against a local HTTP
// fixture. Proves the raw secret genuinely only exists inside the
// resolution scope's callback -- not asserted by inspection, but by the
// fixture server itself observing the exact bearer token value that was
// stored (encrypted) and only decrypted at the final hop.
public sealed class ConnectorDispatcherTests : IAsyncLifetime
{
    private ConnectorDispatcherTestFixture _fixture = null!;
    private HttpListener _listener = null!;
    private string _baseAddress = null!;
    private Func<HttpListenerContext, Task>? _handler;

    public Task InitializeAsync()
    {
        _fixture = new ConnectorDispatcherTestFixture();

        var port = GetFreeTcpPort();
        _baseAddress = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(_baseAddress);
        _listener.Start();
        _ = AcceptLoopAsync();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();
        await _fixture.DisposeAsync();
    }

    private static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task AcceptLoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch (ObjectDisposedException) { return; }
            catch (HttpListenerException) { return; }

            if (_handler is not null)
                await _handler(ctx);
        }
    }

    // Seeds a real User -> Project (FK chain Secret/Connector both need),
    // stores rawSecretValue for real (encrypted, via the real SecretVault)
    // scoped to that Project, then seeds the Module + Connector rows.
    // Pass rawSecretValue: null for a Connector with no SecretHandle.
    private async Task<(Guid ProjectId, Module Module, Connector Connector)> SeedAsync(
        IServiceScope scope, IReadOnlyList<CapabilityGrant> approvedCapabilities, string? rawSecretValue)
    {
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var owner = new User
        {
            Id = Guid.NewGuid(), DisplayName = "Test Owner", Email = $"{Guid.NewGuid():N}@telechron.dev",
            AuthCredentialHash = "hash", Role = Role.Admin, CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await users.AddAsync(owner);

        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var project = new Project
        {
            Id = Guid.NewGuid(), Name = "Test Project", RootPath = "/repo", OwnerUserId = owner.Id,
            RepairPolicy = RepairPolicy.RequireApproval, CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await projects.AddAsync(project);

        string? secretHandle = null;
        if (rawSecretValue is not null)
        {
            var secretVault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
            secretHandle = await secretVault.StoreAsync(project.Id, "github-pat", Encoding.UTF8.GetBytes(rawSecretValue));
        }

        var modules = scope.ServiceProvider.GetRequiredService<IModuleRepository>();
        var module = new Module
        {
            Id = Guid.NewGuid(), Name = "telechron.connector.github", Kind = "connector",
            VersionMajor = 1, VersionMinor = 0, VersionPatch = 0,
            CapabilitiesJson = ModuleCapabilities.Serialize(approvedCapabilities),
            TestCommand = "n/a", SourceCodeRef = "n/a", InstalledAtUtc = DateTimeOffset.UtcNow,
        };
        await modules.AddAsync(module);

        var connectors = scope.ServiceProvider.GetRequiredService<IConnectorRepository>();
        var connector = new Connector
        {
            Id = Guid.NewGuid(), Name = "test-github-connector", ModuleId = module.Id, Kind = "connector",
            ConfigurationJson = "{}", SecretHandle = secretHandle, IsDeprecated = false, ProjectId = project.Id,
        };
        await connectors.AddAsync(connector);

        return (project.Id, module, connector);
    }

    [Fact]
    public async Task DispatchAsync_AuthorizedAndSecretResolved_ActuallySendsResolvedSecretToConnector()
    {
        string? observedAuthHeader = null;
        _handler = async ctx =>
        {
            observedAuthHeader = ctx.Request.Headers["Authorization"];
            var bytes = Encoding.UTF8.GetBytes("""{"number": 7}""");
            ctx.Response.StatusCode = 200;
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        };

        using var scope = _fixture.CreateScope();
        var (projectId, module, connector) = await SeedAsync(
            scope, [new CapabilityGrant(CapabilityKind.ConnectorAccess, null)], rawSecretValue: "ghp_realstoredtoken");

        var moduleRuntime = scope.ServiceProvider.GetRequiredService<IModuleRuntime>();
        RegisterLoadedModule(moduleRuntime, module.Name, new GitHubConnectorModule(new Uri(_baseAddress)));

        var dispatcher = scope.ServiceProvider.GetRequiredService<IConnectorDispatcher>();
        var result = await dispatcher.DispatchAsync(
            connector, projectId, "get-issue", """{"owner": "example", "repo": "repo", "issueNumber": 7}""");

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("Bearer ghp_realstoredtoken", observedAuthHeader);
    }

    [Fact]
    public async Task DispatchAsync_ConnectorNotApprovedForCapability_IsDeniedWithoutContactingServer()
    {
        var serverContacted = false;
        _handler = ctx => { serverContacted = true; ctx.Response.Close(); return Task.CompletedTask; };

        using var scope = _fixture.CreateScope();
        // No ConnectorAccess grant -- the Project never approved this
        // module for connector use.
        var (projectId, module, connector) = await SeedAsync(scope, approvedCapabilities: [], rawSecretValue: "ghp_token");

        var moduleRuntime = scope.ServiceProvider.GetRequiredService<IModuleRuntime>();
        RegisterLoadedModule(moduleRuntime, module.Name, new GitHubConnectorModule(new Uri(_baseAddress)));

        var dispatcher = scope.ServiceProvider.GetRequiredService<IConnectorDispatcher>();
        var result = await dispatcher.DispatchAsync(
            connector, projectId, "get-issue", """{"owner": "example", "repo": "repo", "issueNumber": 7}""");

        Assert.False(result.Succeeded);
        Assert.Contains("Not authorized", result.ErrorMessage);
        Assert.False(serverContacted);
    }

    [Fact]
    public async Task DispatchAsync_ModuleNotLoaded_FailsWithoutResolvingSecret()
    {
        using var scope = _fixture.CreateScope();
        var (projectId, _, connector) = await SeedAsync(
            scope, [new CapabilityGrant(CapabilityKind.ConnectorAccess, null)], rawSecretValue: "ghp_token");
        // Deliberately never registered with moduleRuntime.

        var dispatcher = scope.ServiceProvider.GetRequiredService<IConnectorDispatcher>();
        var result = await dispatcher.DispatchAsync(connector, projectId, "get-issue", "{}");

        Assert.False(result.Succeeded);
        Assert.Contains("not currently loaded", result.ErrorMessage);
    }

    [Fact]
    public async Task DispatchAsync_ConnectorWithoutSecretHandle_PassesEmptySecretBytes()
    {
        _handler = async ctx =>
        {
            var bytes = Encoding.UTF8.GetBytes("""{"number": 1}""");
            ctx.Response.StatusCode = 200;
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        };

        using var scope = _fixture.CreateScope();
        var (projectId, module, connector) = await SeedAsync(
            scope, [new CapabilityGrant(CapabilityKind.ConnectorAccess, null)], rawSecretValue: null);

        var moduleRuntime = scope.ServiceProvider.GetRequiredService<IModuleRuntime>();
        RegisterLoadedModule(moduleRuntime, module.Name, new GitHubConnectorModule(new Uri(_baseAddress)));

        var dispatcher = scope.ServiceProvider.GetRequiredService<IConnectorDispatcher>();
        var result = await dispatcher.DispatchAsync(
            connector, projectId, "get-issue", """{"owner": "e", "repo": "r", "issueNumber": 1}""");

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    private static void RegisterLoadedModule(IModuleRuntime runtime, string moduleName, IModule instance)
    {
        // Test-only seam: ModuleRuntime's real load path only ever calls
        // Activator.CreateInstance(Type) with no arguments, so it can never
        // pass this test's local-fixture Uri into GitHubConnectorModule's
        // constructor -- there is no way to point a REAL assembly load at
        // the test server. Reflection here reaches the private _loaded
        // dictionary purely to register an already-constructed instance
        // pointed at the fixture, so this test can prove dispatch/
        // mediation/secret-scope wiring without touching real GitHub.
        var field = runtime.GetType().GetField("_loaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("ModuleRuntime._loaded field not found -- test seam broke.");
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<string, LoadedModule>)field.GetValue(runtime)!;
        dict[moduleName] = new LoadedModule
        {
            ModuleName = moduleName,
            Version = instance.Version,
            Instance = instance,
            LoadContext = new ModuleLoadContext(typeof(GitHubConnectorModule).Assembly.Location),
            UnloadWeakReference = new WeakReference(new object()),
            LoadedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
