namespace Telechron.Host.Telemetry;

using Microsoft.AspNetCore.Http;
using Telechron.Sdk.Telemetry;

// R-REL6: establishes/propagates a correlation ID across the human-facing
// HTTP request boundary (inbound header -> AsyncLocal for the duration of
// the request -> echoed back on the response). Scope note: this alone
// does NOT extend the trace ID into the Agent gRPC/container hops --
// DispatchedCommand (Sdk/Persistence/IDispatchQueue.cs) has no trace-id
// field yet, so propagating further requires a proto/dispatch-record
// change, not just a middleware one. TelemetryBatcher.EnqueueEvent already
// accepts a traceId parameter end-to-end for whichever caller has one in
// scope; extending dispatch itself to pass CorrelationContext.TraceId
// through is the next real step for full Host->Agent->Container coverage.
public sealed class CorrelationTracingMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(traceId))
        {
            traceId = Guid.NewGuid().ToString("N");
        }

        CorrelationContext.SetTraceId(traceId);
        context.Response.Headers[HeaderName] = traceId;

        await next(context);
    }
}
