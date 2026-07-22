using Microsoft.Extensions.Logging;
using Telechron.Sdk.Security;

namespace Telechron.Host.Security.Logging;

// R-SEC1: wraps every registered ILoggerProvider so log messages are checked
// against ISecretFingerprintRegistry before reaching any sink (console, file,
// etc.) — the safety net behind the primary discipline of never logging raw
// secret values by construction.
public sealed class RedactingLoggerProvider(ILoggerProvider inner, ISecretFingerprintRegistry fingerprints) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new RedactingLogger(inner.CreateLogger(categoryName), fingerprints);

    public void Dispose() => inner.Dispose();

    private sealed class RedactingLogger(ILogger inner, ISecretFingerprintRegistry fingerprints) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string Formatter(TState s, Exception? ex)
            {
                var message = formatter(s, ex);
                return fingerprints.TryRedact(message, out var redacted) ? redacted : message;
            }

            inner.Log(logLevel, eventId, state, exception, Formatter);
        }
    }
}
