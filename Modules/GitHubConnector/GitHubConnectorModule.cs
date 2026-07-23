using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Telechron.Sdk.Modules;
using Telechron.Sdk.Modules.Connectors;

namespace Telechron.Modules.GitHubConnector;

// R-DM11/R-MOD9: GitHub Connector -- personal access token auth
// ("Bearer" scheme, GitHub REST API v3 convention), operating against
// issues/repository-content artifact types. BaseAddress is overridable
// (defaults to the real API) specifically so this can be pointed at a
// local test fixture server without touching real GitHub credentials --
// per this session's decision to exercise the R-SEC5 secret-boundary
// mechanics against local fixtures rather than live external accounts.
public sealed class GitHubConnectorModule(Uri? baseAddress = null) : IConnectorModule
{
    private readonly Uri _baseAddress = baseAddress ?? new Uri("https://api.github.com/");

    public string Name => "telechron.connector.github";
    public string Kind => "connector";
    public ModuleVersion Version => new(1, 0, 0);
    public IReadOnlyList<string> DeclaredCapabilities => ["ConnectorAccess", "InternetAccess", "SecretAccess"];

    public string AuthenticationMechanism => "personal-access-token";
    public IReadOnlyList<string> SupportedArtifactTypes => ["issue", "repository-content"];
    public IReadOnlyList<string> SupportedOperations => ["get-issue", "create-issue-comment"];

    public async Task<ConnectorOperationResult> ExecuteOperationAsync(
        string operation, string parametersJson, ReadOnlyMemory<byte> secretBytes, CancellationToken ct = default)
    {
        if (!SupportedOperations.Contains(operation))
            return ConnectorOperationResult.Failure($"Unsupported operation '{operation}'. Supported: {string.Join(", ", SupportedOperations)}.");

        JsonElement parameters;
        try
        {
            parameters = JsonDocument.Parse(parametersJson).RootElement;
        }
        catch (JsonException ex)
        {
            return ConnectorOperationResult.Failure($"Invalid parameters JSON: {ex.Message}");
        }

        using var client = new HttpClient { BaseAddress = _baseAddress };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Encoding.UTF8.GetString(secretBytes.Span));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Telechron", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        try
        {
            return operation switch
            {
                "get-issue" => await GetIssueAsync(client, parameters, ct),
                "create-issue-comment" => await CreateIssueCommentAsync(client, parameters, ct),
                _ => ConnectorOperationResult.Failure($"Unhandled operation '{operation}'."),
            };
        }
        catch (HttpRequestException ex)
        {
            return ConnectorOperationResult.Failure($"GitHub API request failed: {ex.Message}");
        }
    }

    private static async Task<ConnectorOperationResult> GetIssueAsync(HttpClient client, JsonElement parameters, CancellationToken ct)
    {
        var owner = parameters.GetProperty("owner").GetString();
        var repo = parameters.GetProperty("repo").GetString();
        var issueNumber = parameters.GetProperty("issueNumber").GetInt32();

        var response = await client.GetAsync($"repos/{owner}/{repo}/issues/{issueNumber}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        return response.IsSuccessStatusCode
            ? ConnectorOperationResult.Success(body)
            : ConnectorOperationResult.Failure($"GitHub API returned {(int)response.StatusCode}: {body}");
    }

    private static async Task<ConnectorOperationResult> CreateIssueCommentAsync(HttpClient client, JsonElement parameters, CancellationToken ct)
    {
        var owner = parameters.GetProperty("owner").GetString();
        var repo = parameters.GetProperty("repo").GetString();
        var issueNumber = parameters.GetProperty("issueNumber").GetInt32();
        var body = parameters.GetProperty("body").GetString();

        var payload = JsonSerializer.Serialize(new { body });
        var response = await client.PostAsync(
            $"repos/{owner}/{repo}/issues/{issueNumber}/comments",
            new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        return response.IsSuccessStatusCode
            ? ConnectorOperationResult.Success(responseBody)
            : ConnectorOperationResult.Failure($"GitHub API returned {(int)response.StatusCode}: {responseBody}");
    }

    public Task<ModuleSelfTestResult> RunSelfTestAsync(CancellationToken ct = default)
    {
        var errors = new List<string>();
        if (AuthenticationMechanism != "personal-access-token") errors.Add("Unexpected AuthenticationMechanism.");
        if (SupportedOperations.Count == 0) errors.Add("SupportedOperations must not be empty.");
        if (!DeclaredCapabilities.Contains("SecretAccess")) errors.Add("A Connector must declare SecretAccess.");

        return Task.FromResult(errors.Count == 0
            ? ModuleSelfTestResult.Success("GitHub connector descriptor is internally consistent.")
            : ModuleSelfTestResult.Failure("GitHub connector self-consistency check failed.", [.. errors]));
    }
}
