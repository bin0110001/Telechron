namespace Telechron.Sdk.Telemetry;

public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> CurrentTraceId = new();

    public static string TraceId
    {
        get => CurrentTraceId.Value ?? SetTraceId(Guid.NewGuid().ToString("N"));
        set => CurrentTraceId.Value = value;
    }

    public static string SetTraceId(string traceId)
    {
        CurrentTraceId.Value = traceId;
        return traceId;
    }
}
