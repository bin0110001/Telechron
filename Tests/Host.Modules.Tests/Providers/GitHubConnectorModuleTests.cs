using System.Net;
using System.Text;
using System.Text.Json;
using Telechron.Modules.GitHubConnector;

namespace Telechron.Host.Modules.Tests.Providers;

// Exercises the GitHub connector against a real local HTTP server
// (HttpListener) rather than the live GitHub API -- proves the actual
// request construction (auth header, path, JSON body) and response
// handling work end to end, without needing a real GitHub account/PAT.
public class GitHubConnectorModuleTests : IAsyncLifetime
{
    private HttpListener _listener = null!;
    private string _baseAddress = null!;
    private Func<HttpListenerContext, Task>? _handler;

    public Task InitializeAsync()
    {
        var port = GetFreeTcpPort();
        _baseAddress = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(_baseAddress);
        _listener.Start();
        _ = AcceptLoopAsync();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();
        return Task.CompletedTask;
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

    private GitHubConnectorModule CreateConnector() => new(new Uri(_baseAddress));

    [Fact]
    public async Task ExecuteOperationAsync_GetIssue_SendsBearerAuthAndReturnsBody()
    {
        string? capturedAuthHeader = null;
        string? capturedPath = null;
        _handler = async ctx =>
        {
            capturedAuthHeader = ctx.Request.Headers["Authorization"];
            capturedPath = ctx.Request.Url!.AbsolutePath;
            var body = """{"number": 42, "title": "Test issue"}""";
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = 200;
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        };

        var connector = CreateConnector();
        var secretBytes = Encoding.UTF8.GetBytes("ghp_faketoken123");
        var parameters = """{"owner": "example", "repo": "repo", "issueNumber": 42}""";

        var result = await connector.ExecuteOperationAsync("get-issue", parameters, secretBytes);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("Bearer ghp_faketoken123", capturedAuthHeader);
        Assert.Equal("/repos/example/repo/issues/42", capturedPath);

        using var doc = JsonDocument.Parse(result.OutputJson);
        Assert.Equal(42, doc.RootElement.GetProperty("number").GetInt32());
    }

    [Fact]
    public async Task ExecuteOperationAsync_CreateIssueComment_SendsCorrectBody()
    {
        string? capturedRequestBody = null;
        _handler = async ctx =>
        {
            using var reader = new StreamReader(ctx.Request.InputStream);
            capturedRequestBody = await reader.ReadToEndAsync();
            var responseBytes = Encoding.UTF8.GetBytes("""{"id": 1}""");
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = 201;
            await ctx.Response.OutputStream.WriteAsync(responseBytes);
            ctx.Response.Close();
        };

        var connector = CreateConnector();
        var parameters = """{"owner": "example", "repo": "repo", "issueNumber": 1, "body": "hello from telechron"}""";

        var result = await connector.ExecuteOperationAsync("create-issue-comment", parameters, Encoding.UTF8.GetBytes("token"));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(capturedRequestBody);
        using var doc = JsonDocument.Parse(capturedRequestBody!);
        Assert.Equal("hello from telechron", doc.RootElement.GetProperty("body").GetString());
    }

    [Fact]
    public async Task ExecuteOperationAsync_ApiReturnsError_IsSurfacedAsFailure()
    {
        _handler = async ctx =>
        {
            var bytes = Encoding.UTF8.GetBytes("""{"message": "Not Found"}""");
            ctx.Response.StatusCode = 404;
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        };

        var connector = CreateConnector();
        var result = await connector.ExecuteOperationAsync(
            "get-issue", """{"owner": "x", "repo": "y", "issueNumber": 999}""", Encoding.UTF8.GetBytes("token"));

        Assert.False(result.Succeeded);
        Assert.Contains("404", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteOperationAsync_UnsupportedOperation_IsRejectedWithoutContactingServer()
    {
        var serverContacted = false;
        _handler = ctx => { serverContacted = true; ctx.Response.Close(); return Task.CompletedTask; };

        var connector = CreateConnector();
        var result = await connector.ExecuteOperationAsync("delete-repository", "{}", Encoding.UTF8.GetBytes("token"));

        Assert.False(result.Succeeded);
        Assert.False(serverContacted);
    }

    [Fact]
    public async Task RunSelfTestAsync_Passes()
    {
        var connector = CreateConnector();

        var result = await connector.RunSelfTestAsync();

        Assert.True(result.Passed, string.Join("; ", result.Errors));
    }
}
