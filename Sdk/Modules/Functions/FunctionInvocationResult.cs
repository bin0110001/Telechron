namespace Telechron.Sdk.Modules.Functions;

public sealed record FunctionInvocationResult(bool Succeeded, string OutputArtifactTypesJson, string OutputSummary, string? ErrorMessage)
{
    public static FunctionInvocationResult Success(string outputArtifactTypesJson, string summary) =>
        new(true, outputArtifactTypesJson, summary, null);

    public static FunctionInvocationResult Failure(string error) => new(false, "[]", string.Empty, error);
}
