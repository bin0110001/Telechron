namespace Telechron.Host.Telemetry;

using System.Collections.Concurrent;
using Telechron.Sdk.Telemetry;

public sealed class TelemetryBatcher : ITelemetryBatcher
{
    private readonly ConcurrentQueue<TelemetryEvent> _events = new();

    public void EnqueueEvent(string eventName, string category, string traceId, string payloadJson)
    {
        _events.Enqueue(new TelemetryEvent
        {
            Id = Guid.NewGuid(),
            EventName = eventName,
            Category = category,
            TraceId = traceId,
            PayloadJson = payloadJson,
            TimestampUtc = DateTimeOffset.UtcNow
        });
    }

    public Task<int> FlushAsync(CancellationToken ct = default)
    {
        var count = 0;
        while (_events.TryDequeue(out _))
        {
            count++;
        }
        return Task.FromResult(count);
    }

    public int PendingCount => _events.Count;
}
