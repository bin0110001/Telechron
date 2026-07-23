namespace Telechron.Sdk.Telemetry;

public sealed record TelemetryEvent
{
    public required Guid Id { get; init; }
    public required string EventName { get; init; }
    public required string Category { get; init; }
    public required string TraceId { get; init; }
    public required string PayloadJson { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
}

public interface ITelemetryBatcher
{
    void EnqueueEvent(string eventName, string category, string traceId, string payloadJson);
    Task<int> FlushAsync(CancellationToken ct = default);
    int PendingCount { get; }
}
