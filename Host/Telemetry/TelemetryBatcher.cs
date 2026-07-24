namespace Telechron.Host.Telemetry;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Telechron.Sdk.Telemetry;

// R-PER5: buffers telemetry events in memory and flushes them in a batch
// via structured logging rather than a synchronous per-event SQLite write
// -- there is no dedicated telemetry table in the domain model (unlike
// Run/Finding/LlmCall), so structured logs are the real, working sink here
// rather than inventing a new EF migration for what remains an
// operational-observability concern, not queryable domain data.
public sealed class TelemetryBatcher(ILogger<TelemetryBatcher> logger) : ITelemetryBatcher
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
        while (_events.TryDequeue(out var telemetryEvent))
        {
            logger.LogInformation(
                "Telemetry [{Category}/{EventName}] trace={TraceId} at={TimestampUtc}: {PayloadJson}",
                telemetryEvent.Category, telemetryEvent.EventName, telemetryEvent.TraceId,
                telemetryEvent.TimestampUtc, telemetryEvent.PayloadJson);
            count++;
        }
        return Task.FromResult(count);
    }

    public int PendingCount => _events.Count;
}
