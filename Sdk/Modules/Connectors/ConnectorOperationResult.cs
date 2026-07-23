namespace Telechron.Sdk.Modules.Connectors;

public sealed record ConnectorOperationResult(bool Succeeded, string OutputJson, string? ErrorMessage)
{
    public static ConnectorOperationResult Success(string outputJson) => new(true, outputJson, null);
    public static ConnectorOperationResult Failure(string error) => new(false, "{}", error);
}
