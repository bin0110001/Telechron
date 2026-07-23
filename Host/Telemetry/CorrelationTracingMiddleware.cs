namespace Telechron.Host.Telemetry;

using Microsoft.AspNetCore.Http;
using Telechron.Sdk.Telemetry;

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
